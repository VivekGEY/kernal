import pytest
import semantic_kernel.connectors.ai.open_ai as sk_oai
from semantic_kernel.kernel import Kernel
from semantic_kernel.planning.sequential_planner.config import SequentialPlannerConfig
from semantic_kernel.planning.sequential_planner.planner import SequentialPlanner
from tests.integration.fakes.email_skill_fake import EmailSkillFake
from tests.integration.fakes.fun_skill_fake import FunSkillFake
from tests.integration.fakes.writer_skill_fake import WriterSkillFake


def initialize_kernel(get_aoai_config, use_embeddings=False, use_chat_model=False):
    _, api_key, endpoint = get_aoai_config

    kernel = Kernel()
    if use_chat_model:
        kernel.add_chat_service(
            "chat_completion",
            sk_oai.AzureChatCompletion("gpt-35-turbo", endpoint, api_key),
        )
    else:
        kernel.add_text_completion_service(
            "text_completion",
            sk_oai.AzureChatCompletion("gpt-35-turbo", endpoint, api_key),
        )

    if use_embeddings:
        kernel.add_text_embedding_generation_service(
            "text_embedding",
            sk_oai.AzureTextEmbedding("text-embedding-ada-002", endpoint, api_key),
        )
    return kernel


@pytest.mark.parametrize(
    "use_chat_model, prompt, expected_function, expected_skill",
    [
        (
            False,
            "Write a joke and send it in an e-mail to Kai.",
            "SendEmail",
            "_GLOBAL_FUNCTIONS_",
        ),
        (
            True,
            "Write a joke and send it in an e-mail to Kai.",
            "SendEmail",
            "_GLOBAL_FUNCTIONS_",
        ),
    ],
)
@pytest.mark.asyncio
async def test_create_plan_function_flow_async(
    get_aoai_config, use_chat_model, prompt, expected_function, expected_skill
):
    # Arrange
    kernel = initialize_kernel(get_aoai_config, False, use_chat_model)
    kernel.import_skill(EmailSkillFake())
    kernel.import_skill(FunSkillFake())

    planner = SequentialPlanner(kernel)

    # Act
    plan = await planner.create_plan_async(prompt)

    # Assert
    assert any(
        step.name == expected_function and step.skill_name == expected_skill
        for step in plan._steps
    )


@pytest.mark.parametrize(
    "prompt, expected_function, expected_skill, expected_default",
    [
        (
            "Write a novel outline.",
            "NovelOutline",
            "WriterSkill",
            "<!--===ENDPART===-->",
        )
    ],
)
@pytest.mark.asyncio
async def test_create_plan_with_defaults_async(
    get_aoai_config, prompt, expected_function, expected_skill, expected_default
):
    # Arrange
    kernel = initialize_kernel(get_aoai_config)
    kernel.import_skill(EmailSkillFake())
    kernel.import_skill(WriterSkillFake(), "WriterSkill")

    planner = SequentialPlanner(kernel)

    # Act
    plan = await planner.create_plan_async(prompt)

    # Assert
    assert any(
        step.name == expected_function and step.skill_name == expected_skill
        # and step.parameters["endMarker"] == expected_default # TODO: Check endMarker has been set in C#
        for step in plan._steps
    )


@pytest.mark.parametrize(
    "prompt, expected_function, expected_skill",
    [
        (
            "Write a poem or joke and send it in an e-mail to Kai.",
            "SendEmail",
            "_GLOBAL_FUNCTIONS_",
        )
    ],
)
@pytest.mark.asyncio
async def test_create_plan_goal_relevant_async(
    get_aoai_config, prompt, expected_function, expected_skill
):
    # Arrange
    kernel = initialize_kernel(get_aoai_config, use_embeddings=True)
    kernel.import_skill(EmailSkillFake())
    kernel.import_skill(FunSkillFake())
    kernel.import_skill(WriterSkillFake())

    planner = SequentialPlanner(
        kernel,
        SequentialPlannerConfig(relevancy_threshold=0.65, max_relevant_functions=30),
    )

    # Act
    plan = await planner.create_plan_async(prompt)

    # Assert
    assert any(
        step.name == expected_function and step.skill_name == expected_skill
        for step in plan._steps
    )
