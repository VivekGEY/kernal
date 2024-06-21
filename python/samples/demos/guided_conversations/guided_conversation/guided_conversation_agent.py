"""Main orchestrator"""

from collections.abc import Callable
from dataclasses import dataclass, field
from enum import Enum
import logging

from pydantic import BaseModel
from semantic_kernel import Kernel

from guided_conversation.functions.conversation_plan import conversation_plan_function
from guided_conversation.functions.execution import end_conversation, execution, send_message
from guided_conversation.functions.final_update_plan import final_update_plan_function
from guided_conversation.plugins.agenda import Agenda
from guided_conversation.plugins.artifact import Artifact
from guided_conversation.utils.conversation_helpers import Conversation, ConversationMessage, ConversationMessageType
from guided_conversation.utils.openai_tool_calling import (
    ToolValidationResult,
    parse_function_result,
    validate_tool_calling,
)
from guided_conversation.utils.plugin_helpers import PluginOutput, format_kernel_functions_as_tools
from guided_conversation.utils.resources import GCResource, ResourceConstraint

MAX_DECISION_RETRIES = 2


class ToolName(Enum):
    UPDATE_ARTIFACT_TOOL = "update_artifact_field"
    UPDATE_AGENDA_TOOL = "update_agenda"
    SEND_MSG_TOOL = "send_message_to_user"
    END_CONV_TOOL = "end_conversation"


@dataclass
class GCInput:
    artifact: BaseModel
    rules: list[str]
    conversation_flow: str | None
    context: str | None
    resource_constraint: ResourceConstraint | None


@dataclass
class GCOutput:
    """The output of the GuidedConversation agent.

    Args:
        ai_message (str): The message to send to the user.
        is_conversation_over (bool): Whether the conversation is over.
    """

    ai_message: str | None = field(default=None)
    is_conversation_over: bool = field(default=False)


class GuidedConversation:
    def __init__(self, gc_input: GCInput, kernel: Kernel, service_id: str = "gc_main") -> None:
        """Initializes the GuidedConversation agent.

        Args:
            gc_input (GCInput): The input to the guided conversation.
            kernel (Kernel): An instance of Kernel. Must come initialized with a AzureOpenAI or OpenAI service.
            service_id (str): Provide a service_id associated with the kernel's service that was provided.
        """

        self.logger = logging.getLogger(__name__)
        self.kernel = kernel
        self.service_id = service_id
        self.gc_input = gc_input

        self.chat_history = Conversation()
        self.resource = GCResource(gc_input.resource_constraint)
        self.artifact = Artifact(self.kernel, self.service_id, gc_input.artifact)
        self.agenda = Agenda(self.kernel, self.service_id, self.resource.get_resource_mode(), MAX_DECISION_RETRIES)

        # Plugins will be executed in the order of this list.
        self.plugins_order = [
            ToolName.UPDATE_ARTIFACT_TOOL.value,
            ToolName.UPDATE_AGENDA_TOOL.value,
        ]

        # Terminal plugins are plugins that are handled in a special way:
        # - Only one terminal plugin can be called in a single step of the conversation as it leads to the end of the conversation step.
        # - The order of this list determines the execution priority.
        # - For example, if the model chooses to both call send message and end conversation,
        #   Send message will be executed first and since the orchestration step returns, end conversation will not be executed.
        self.terminal_plugins_order = [
            ToolName.SEND_MSG_TOOL.value,
            ToolName.END_CONV_TOOL.value,
        ]

        self.current_failed_decision_attempts = 0

        # Set common request settings
        self.req_settings = self.kernel.get_prompt_execution_settings_from_service_id(self.service_id)
        self.req_settings.max_tokens = 2000
        self.kernel.add_function(plugin_name=ToolName.SEND_MSG_TOOL.value, function=send_message)
        self.kernel.add_function(plugin_name=ToolName.END_CONV_TOOL.value, function=end_conversation)
        self.kernel.add_function(
            plugin_name=ToolName.UPDATE_ARTIFACT_TOOL.value, function=self.artifact.update_artifact_field
        )
        self.kernel.add_function(
            plugin_name=ToolName.UPDATE_AGENDA_TOOL.value, function=self.agenda.update_agenda_items
        )

    async def step_conversation(self, user_input: str | None = None) -> GCOutput:
        """Given a message from a user, this will execute the guided conversation agent up until a
        terminal plugin is called or the maximum number of decision retries is reached."""
        self.logger.info(f"Starting conversation step {self.resource.get_turn_number()}.")
        self.resource.start_resource()
        self.current_failed_decision_attempts = 0
        if user_input:
            self.chat_history.add_messages(
                ConversationMessage(role="user", content=user_input, turn_number=self.resource.get_turn_number())
            )

        # Keep generating and executing plans until a terminal plugin is called
        # or the maximum number of decision retries is reached.
        while self.current_failed_decision_attempts < MAX_DECISION_RETRIES:
            plan = await self._generate_plan()
            success, plugins, terminal_plugins = await self._execute_plan(plan)

            if success != ToolValidationResult.SUCCESS:
                self.logger.warning(
                    f"Failed to parse tools in plan on retry attempt {self.current_failed_decision_attempts} out of {MAX_DECISION_RETRIES}."
                )
                self.current_failed_decision_attempts += 1
                continue

            # Run a step of the orchestration logic based on the plugins called by the model.
            # First execute all regular plugins (if any) in the order returned by _execute_plan
            for plugin_name, plugin_args in plugins:
                if plugin_name == f"{ToolName.UPDATE_ARTIFACT_TOOL.value}-{ToolName.UPDATE_ARTIFACT_TOOL.value}":
                    plugin_args["conversation"] = self.chat_history
                    # Modify plugin_args such that field=field_name and value=field_value
                    plugin_args["field_name"] = plugin_args.pop("field")
                    plugin_args["field_value"] = plugin_args.pop("value")
                    await self._call_plugin(self.artifact.update_artifact, plugin_args)
                elif plugin_name == f"{ToolName.UPDATE_AGENDA_TOOL.value}-{ToolName.UPDATE_AGENDA_TOOL.value}":
                    plugin_args["remaining_turns"] = self.resource.get_remaining_turns()
                    plugin_args["conversation"] = self.chat_history
                    await self._call_plugin(self.agenda.update_agenda, plugin_args)

            # Then execute the first terminal plugin (if any)
            if terminal_plugins:
                gc_output = GCOutput()
                plugin_name, plugin_args = terminal_plugins[0]
                if plugin_name == f"{ToolName.SEND_MSG_TOOL.value}-{ToolName.SEND_MSG_TOOL.value}":
                    gc_output.ai_message = plugin_args["message"]
                elif plugin_name == f"{ToolName.END_CONV_TOOL.value}-{ToolName.END_CONV_TOOL.value}":
                    await self._final_update()
                    gc_output.ai_message = "I will terminate this conversation now. Thank you for your time!"
                    gc_output.is_conversation_over = True
                self.resource.increment_resource()
                return gc_output

        # Handle case where the maximum number of decision retries was reached.
        self.logger.warning(f"Failed to execute plan after {MAX_DECISION_RETRIES} attempts.")
        self.resource.increment_resource()
        gc_output = GCOutput()
        gc_output.ai_message = "An error occurred and I must sadly end the conversation."
        gc_output.is_conversation_over = True
        return gc_output

    async def _call_plugin(self, plugin_function: Callable, plugin_args: dict):
        """Common logic whenever any plugin is called like handling errors and appending to chat history."""
        self.logger.info(f"Calling plugin {plugin_function.__name__}.")
        output: PluginOutput = await plugin_function(**plugin_args)
        if output.update_successful:
            # Set turn numbers
            for message in output.messages:
                message.turn_number = self.resource.get_turn_number()
            self.chat_history.add_messages(output.messages)
        else:
            self.logger.warning(
                f"Plugin {plugin_function.__name__} failed to execute on attempt {self.current_failed_decision_attempts} out of {MAX_DECISION_RETRIES}."
            )
            self.current_failed_decision_attempts += 1

    async def _generate_plan(self) -> str:
        """Generate a plan for the current state of the conversation. The idea here is to explictly let the model plan before
        generating any plugin calls. This has been shown to increase reliability.

        Returns:
            str: The plan generated by the plan function.
        """
        self.logger.info("Generating plan for the current state of the conversation")
        plan = await conversation_plan_function(
            self.kernel,
            self.chat_history,
            self.gc_input.context,
            self.gc_input.rules,
            self.gc_input.conversation_flow,
            self.artifact,
            self.req_settings,
            self.resource,
            self.agenda,
        )
        plan = plan.value[0].content
        self.chat_history.add_messages(
            ConversationMessage(
                role="assistant",
                content=plan,
                turn_number=self.resource.turn_number,
                type=ConversationMessageType.REASONING,
            )
        )
        return plan

    async def _execute_plan(
        self, plan: str
    ) -> tuple[ToolValidationResult, list[tuple[str, dict]], list[tuple[str, dict]]]:
        """Given the generated plan by the model, use that plan to generate which functions to execution.
        Once the tool calls are generated by the model, we sort them into two groups: regular plugins and terminal plugins
        according to the definition in __init__

        Args:
            plan (str): The plan generated by the model.

        Returns:
            tuple[ToolValidationResult, list[tuple[str, dict]], list[tuple[str, dict]]]: A tuple containing the validation result
            of the tool calls, the regular plugins to execute, and the terminal plugins to execute alongside their arguments.
        """
        self.logger.info("Executing plan.")

        req_settings = self.kernel.get_prompt_execution_settings_from_service_id(self.service_id)
        functions = self.plugins_order + self.terminal_plugins_order
        result = await execution(
            kernel=self.kernel,
            reasoning=plan,
            filter=functions,
            req_settings=req_settings,
            artifact_schema=self.artifact.get_schema_for_prompt(),
        )

        parsed_result = parse_function_result(result)
        formatted_tools = format_kernel_functions_as_tools(self.kernel, functions)
        validation_result = validate_tool_calling(parsed_result, formatted_tools)

        # Sort plugin calls into two groups in the order of the corresponding lists defined in __init__
        plugins = []
        terminal_plugins = []
        if validation_result == ToolValidationResult.SUCCESS:
            for plugin in self.plugins_order:
                for idx, called_plugin_name in enumerate(parsed_result["tool_names"]):
                    plugin_name = f"{plugin}-{plugin}"
                    if called_plugin_name == plugin_name:
                        plugins.append((parsed_result["tool_names"][idx], parsed_result["tool_args_list"][idx]))

            for terminal_plugin in self.terminal_plugins_order:
                for idx, called_plugin_name in enumerate(parsed_result["tool_names"]):
                    terminal_plugin_name = f"{terminal_plugin}-{terminal_plugin}"
                    if called_plugin_name == terminal_plugin_name:
                        terminal_plugins.append(
                            (parsed_result["tool_names"][idx], parsed_result["tool_args_list"][idx])
                        )

        return validation_result, plugins, terminal_plugins

    async def _final_update(self):
        """Explicit final update of the artifact after the conversation ends."""
        self.logger.info("Final update of the artifact prior to terminating the conversation.")

        # Get a plan from the model
        reasoning_response = await final_update_plan_function(
            kernel=self.kernel,
            req_settings=self.req_settings,
            chat_history=self.chat_history,
            context=self.gc_input.context,
            artifact_schema=self.artifact.get_schema_for_prompt(),
            artifact_state=self.artifact.get_artifact_for_prompt(),
        )

        # Then generate the functions to be executed
        req_settings = self.kernel.get_prompt_execution_settings_from_service_id(self.service_id)
        req_settings.tool_choice = "auto"
        functions = [ToolName.UPDATE_ARTIFACT_TOOL.value]
        execution_response = await execution(
            kernel=self.kernel,
            reasoning=reasoning_response.value[0].content,
            filter=functions,
            req_settings=req_settings,
            artifact_schema=self.artifact.get_schema_for_prompt(),
        )

        parsed_result = parse_function_result(execution_response)
        formatted_tools = format_kernel_functions_as_tools(self.kernel, functions)
        validation_result = validate_tool_calling(parsed_result, formatted_tools)

        # If the tool call was successful, update the artifact.
        if validation_result != ToolValidationResult.SUCCESS:
            self.logger.warning(f"No artifact change during final update due to: {validation_result.value}")
            pass
        else:
            for i in range(len(parsed_result["tool_names"])):
                tool_name = parsed_result["tool_names"][i]
                tool_args = parsed_result["tool_args_list"][i]
                if tool_name == f"{ToolName.UPDATE_ARTIFACT_TOOL.value}-{ToolName.UPDATE_ARTIFACT_TOOL.value}":
                    plugin_output = await self.artifact.update_artifact(
                        field_name=tool_args["field_name"],
                        field_value=tool_args["field_value"],
                        conversation=self.chat_history,
                    )
                    if plugin_output.update_successful:
                        self.logger.info(f"Artifact field {tool_args['field_name']} successfully updated.")
                        # Set turn numbers
                        for message in plugin_output.messages:
                            message.turn_number = self.resource.get_turn_number()
                        self.chat_history.add_messages(plugin_output.messages)
                    else:
                        self.logger.error(f"Final artifact field update of {tool_args['field_name']} failed.")

    def to_json(self) -> dict:
        return {
            "artifact": self.artifact.to_json(),
            "agenda": self.agenda.to_json(),
            "chat_history": self.chat_history.to_json(),
            "resource": self.resource.to_json(),
        }

    @classmethod
    def from_json(
        cls,
        json_data: dict,
        gc_input: GCInput,
        kernel: Kernel,
        service_id: str = "gc_main",
    ) -> "GuidedConversation":
        artifact = Artifact.from_json(
            json_data["artifact"],
            kernel=kernel,
            service_id=service_id,
            input_artifact=gc_input.artifact,
            max_artifact_field_retries=MAX_DECISION_RETRIES,
        )
        agenda = Agenda.from_json(
            json_data["agenda"],
            kernel=kernel,
            service_id=service_id,
            resource_constraint_mode=gc_input.resource_constraint.mode,
        )
        chat_history = Conversation.from_json(json_data["chat_history"])
        resource = GCResource.from_json(json_data["resource"])

        gc = cls(gc_input, kernel, service_id)
        gc.artifact = artifact
        gc.agenda = agenda
        gc.chat_history = chat_history
        gc.resource = resource

        return gc
