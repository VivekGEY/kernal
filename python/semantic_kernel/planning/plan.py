import json
import re
import regex
from typing import Any, Callable, List, Union, Optional
from logging import Logger
from semantic_kernel import Kernel
from python.semantic_kernel.kernel_exception import KernelException
from semantic_kernel.orchestration.sk_function_base import SKFunctionBase
from semantic_kernel.orchestration.context_variables import ContextVariables
from semantic_kernel.orchestration.sk_context import SKContext
from semantic_kernel.connectors.ai import CompleteRequestSettings
from python.semantic_kernel.memory.semantic_text_memory_base import SemanticTextMemoryBase

from python.semantic_kernel.skill_definition.function_view import FunctionView
from python.semantic_kernel.skill_definition.parameter_view import ParameterView


class Plan(SKFunctionBase):
    _state: ContextVariables
    _steps: List["Plan"]    
    _function: SKFunctionBase
    _parameters: ContextVariables
    _outputs: List[str]
    _has_next_step: bool
    _next_step_index: int
    _name: str
    _skill_name: str
    _description: str
    _is_semantic: bool
    _request_settings: CompleteRequestSettings
    DEFAULT_RESULT_KEY = "PLAN.RESULT"

    @property
    def name(self) -> str:
        return self._name
    
    @property
    def state(self) -> ContextVariables:
        return self._state

    @property
    def skill_name(self) -> str:
        return self._skill_name

    @property
    def description(self) -> str:
        return self._description
    
    @property
    def function(self) -> Callable[..., Any]:
        return self._function

    @property
    def parameters(self) -> List[ParameterView]:
        return self._parameters

    @property
    def is_semantic(self) -> bool:
        return self._is_semantic

    @property
    def is_native(self) -> bool:
        return not self._is_semantic

    @property
    def request_settings(self) -> CompleteRequestSettings:
        return self._request_settings
    
    @property
    def has_next_step(self) -> bool:
        return self._next_step_index < len(self._steps)
    
    @property
    def next_step_index(self) -> int:
        return self._next_step_index


    def __init__(
        self,
        name: Optional[str] = None,
        skill_name: Optional[str] = None,
        description: Optional[str] = None,
        next_step_index: Optional[int] = None,
        state: Optional[ContextVariables] = None,
        parameters: Optional[ContextVariables] = None,
        outputs: Optional[List[str]] = None,
        steps: Optional[List["Plan"]] = None,
        function: Optional[SKFunctionBase] = None,
    ) -> None:
        self._name = "" if name is None else name
        self._skill_name = "" if skill_name is None else skill_name
        self._description = "" if description is None else description
        self._next_step_index = 0 if next_step_index is None else next_step_index
        self._state = ContextVariables() if state is None else state
        self._parameters = ContextVariables() if parameters is None else parameters
        self._outputs = [""] if outputs is None else outputs
        self._steps = [] if steps is None else steps
        self._has_next_step = len(self._steps) > 0
        self._is_semantic = None

        if function is not None:
            self.set_function(function)


    def describe(self) -> FunctionView:
        return self._function.describe()

    def to_json(self, indented: Optional[bool] = False) -> str:
        return json.dumps(self.__dict__, indent=4 if indented else None)

    def from_json(self, json: Any, context: Optional[SKContext] = None) -> "Plan":
        # Filter out good JSON from the input in case additional text is present
        json_regex = r"\{(?:[^{}]|(?R))*\}"
        plan_string = regex.search(json_regex, json).group()
        new_plan = Plan()
        new_plan.__dict__ = json.loads(plan_string)

        if context is None:
            new_plan = self.set_available_functions(new_plan, context)

        return new_plan
    
    def set_available_functions(self, plan: "Plan", context: SKContext) -> "Plan":
        if len(plan.steps) == 0:
            if context.skills is None:
                raise KernelException(
                    KernelException.ErrorCodes.SkillCollectionNotSet,
                    "Skill collection not found in the context")
            try:
                skillFunction = context.skills.get_function(plan.skill_name, plan.name)
                plan.set_function(skillFunction)
            except:
                pass
        else:
            for step in plan.steps:
                step = self.set_available_functions(step, context)

        return plan
    
    def add_steps(self, steps: Optional[List[SKFunctionBase]]) -> None:
        for step in steps:
            if type(step) is Plan:
                self._steps.append(step)
            else:
                new_step = Plan(
                    step.name,
                    step.skill_name,
                    step.description,
                    0,
                    ContextVariables(),
                    ContextVariables(),
                    [],
                    [],
                )
                new_step.set_function(step)
                self._steps.append(new_step)

    def set_function(self, function: SKFunctionBase) -> None:
        self._function = function
        self._name = function.name
        self._skill_name = function.skill_name
        self._description = function.description
        self._is_semantic = function.is_semantic
        self._request_settings = function.request_settings
    
    def run_next_step_async(
        self,
        kernel: Kernel,
        variables: ContextVariables,
    ) -> "Plan":
        context = kernel.create_new_context(variables)
        return self.invoke_next_step(context)

    async def invoke_next_step(self, context: SKContext) -> "Plan":
        if self._has_next_step:
            step = self._steps[self._next_step_index]

            # merge the state with the current context variables for step execution
            variables = self.get_next_step_variables(context.variables, step)

            # Invoke the step
            func_context = SKContext(
                variables=variables,
                memory=context._memory,
                skills=context.skills,
                logger=context.log
            )
            result = await step.invoke_async(func_context)
            result_value = result.result.strip()

            if result.error_occurred:
                raise KernelException(
                    KernelException.ErrorCodes.FunctionInvokeError,
                    "Error occured while running plan step: " + result.last_error_description,
                    result.last_exception
                )
            
            # Update state with result
            self.state.update(result_value)

            # Update plan result in state with matching outputs (if any)
            if set(self._outputs & step._outputs):                
                current_plan_result = ""
                if Plan.DEFAULT_RESULT_KEY in self._state:
                    current_plan_result = self._state[Plan.DEFAULT_RESULT_KEY]
                self._state.set(Plan.DEFAULT_RESULT_KEY, current_plan_result.strip() + result_value)


            # Update state with outputs (if any)
            for output in step._outputs:
                if output in result.variables:
                    self._state.set(output, result.variables[output])
                else:
                    self._state.set(output, result_value)

            # Increment the step
            self._next_step_index += 1

        return self
    
    async def invoke_async(
        self,
        context: SKContext,
        input: Optional[str] = None,
        settings: Optional[CompleteRequestSettings] = None,
        memory: Optional[SemanticTextMemoryBase] = None,
        logger: Optional[Logger] = None,
        # TODO: cancellation_token: CancellationToken,
    ) -> SKContext:
        if input is not None:
            self._state.update(input)
        
        context = SKContext(
            variables=self._state,
            memory=memory,
            logger=logger
        )
        
        if self._function is not None:
            result = await self._function.invoke_async(context=context, settings=settings)
            if result.error_occurred:
                result.log.error(
                    msg="Something went wrong in plan step {0}.{1}:'{2}'".format(
                        self._skill_name,
                        self._name,
                        context.last_error_description
                    )
                )
                return result
            
            context.variables.update(result.result)
        else:
            # loop through steps until completion
            while self._has_next_step:
                function_context = context
                self.add_variables_to_context(self._state, function_context)
                await self.invoke_next_step(function_context)
                self.update_context_with_outputs(context)

        return context

    def add_variables_to_context(
        self,
        variables: ContextVariables,
        context: SKContext
    ) -> None:
        for key in variables:
            if not context.variables.contains_key(key):
                context.variables.set(key, variables[key])

    def update_context_with_outputs(self, context: SKContext) -> None:
        result_string = ""
        if Plan.DEFAULT_RESULT_KEY in self._state:
            result_string = self._state[Plan.DEFAULT_RESULT_KEY]
        else:
            result_string = str(self._state)

        context.variables.update(result_string)

        for item in self._steps[self._next_step_index-1]._outputs:
            if item in self._state:
                context.variables.set(item, self._state[item])
            else:
                context.variables.set(item, result_string)

        return context
    
    def get_next_step_variables(
        self,
        variables: ContextVariables,
        step: "Plan"
    ) -> ContextVariables:
        # Priority for Input
        # - Parameters (expand from variables if needed)
        # - SKContext.Variables
        # - Plan.State
        # - Empty if sending to another plan
        # - Plan.Description
        input_string = ""
        if step.parameters["input"] is not None:
            input_string = self.expand_from_variables(variables, step.parameters["input"])
        elif variables["input"] is not None:
            input_string = variables["input"]
        elif self._state["input"] is not None:
            input_string = self._state["input"]
        elif len(step._steps) > 0:
            input_string = ""
        elif self._description is not None:
            input_string = self._description

        step_variables = ContextVariables(input_string)
        
        # Priority for remaining stepVariables is:
        # - Function Parameters (pull from variables or state by a key value)
        # - Step Parameters (pull from variables or state by a key value)
        function_params = step.describe()
        for param in function_params.parameters:
            if param.name.lower == "input":
                continue
            if step_variables.contains_key(param.name):
                step_variables.set(param.name, variables[param.name])
            elif self._state.contains_key(param.name) and self._state[param.name] is not None:
                step_variables.set(param.name, self._state[param.name])

        for param in step.parameters:
            if step_variables.contains_key(param.name):
                continue

            expanded_value = self.expand_from_variables(variables, param._default_value)
            if expanded_value.lower() == param._default_value.lower():
                step_variables.set(param.name, expanded_value)
            elif variables.contains_key(param.name):
                step_variables.set(param.name, variables[param.name])
            elif self._state.contains_key(param.name):
                step_variables.set(param.name, self._state[param.name])
            else:
                step_variables.set(param.name, expanded_value)

        return step_variables
    
    def expand_from_variables(
        self,
        variables: ContextVariables,
        input_string: str
    ) -> str:
        result = input_string
        variables_regex = r"\$(?P<var>\w+)"
        matches = re.findall(variables_regex, input_string)
        ordered_matches = sorted(matches, key=lambda m: len(m.group("var")), reverse=True)
        
        for match in ordered_matches:
            var_name = match.group("var")
            if variables.contains_key(var_name):
                result = result.replace(f"${var_name}", variables[var_name])
                
        return result
