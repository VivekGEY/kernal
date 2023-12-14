# Copyright (c) Microsoft. All rights reserved.

from semantic_kernel.connectors.ai.ai_request_settings import AIRequestSettings
from semantic_kernel.connectors.ai.open_ai.request_settings.azure_open_ai_request_settings import (
    AzureAISearchDataSources,
    AzureOpenAIChatRequestSettings,
)
from semantic_kernel.connectors.ai.open_ai.request_settings.open_ai_request_settings import (
    OpenAIChatRequestSettings,
)


def test_default_openai_chat_request_settings():
    settings = OpenAIChatRequestSettings()
    assert settings.temperature == 0.0
    assert settings.top_p == 1.0
    assert settings.presence_penalty == 0.0
    assert settings.frequency_penalty == 0.0
    assert settings.max_tokens == 256
    assert settings.stop is None
    assert settings.number_of_responses == 1
    assert settings.logit_bias == {}
    assert settings.messages[0]["content"] == "Assistant is a large language model."
    assert settings.response_format == "text"


def test_custom_openai_chat_request_settings():
    settings = OpenAIChatRequestSettings(
        temperature=0.5,
        top_p=0.5,
        presence_penalty=0.5,
        frequency_penalty=0.5,
        max_tokens=128,
        stop="\n",
        number_of_responses=2,
        logit_bias={"1": 1},
        messages=[{"role": "system", "content": "Hello"}],
    )
    assert settings.temperature == 0.5
    assert settings.top_p == 0.5
    assert settings.presence_penalty == 0.5
    assert settings.frequency_penalty == 0.5
    assert settings.max_tokens == 128
    assert settings.stop == "\n"
    assert settings.number_of_responses == 2
    assert settings.logit_bias == {"1": 1}
    assert settings.messages == [{"role": "system", "content": "Hello"}]


def test_openai_chat_request_settings_from_default_completion_config():
    settings = AIRequestSettings(service_id="test_service")
    chat_settings = OpenAIChatRequestSettings.from_ai_request(settings)
    assert chat_settings.service_id == "test_service"
    assert chat_settings.temperature == 0.0
    assert chat_settings.top_p == 1.0
    assert chat_settings.presence_penalty == 0.0
    assert chat_settings.frequency_penalty == 0.0
    assert chat_settings.max_tokens == 256
    assert chat_settings.stop is None
    assert chat_settings.number_of_responses == 1
    assert chat_settings.logit_bias == {}


def test_openai_chat_request_settings_from_custom_completion_config():
    settings = AIRequestSettings(
        service_id="test_service",
        extension_data={
            "temperature": 0.5,
            "top_p": 0.5,
            "presence_penalty": 0.5,
            "frequency_penalty": 0.5,
            "max_tokens": 128,
            "stop": ["\n"],
            "number_of_responses": 2,
            "logprobs": 1,
            "logit_bias": {"1": 1},
            "messages": [{"role": "system", "content": "Hello"}],
        },
    )
    chat_settings = OpenAIChatRequestSettings.from_ai_request(settings)
    assert chat_settings.temperature == 0.5
    assert chat_settings.top_p == 0.5
    assert chat_settings.presence_penalty == 0.5
    assert chat_settings.frequency_penalty == 0.5
    assert chat_settings.max_tokens == 128
    assert chat_settings.stop == ["\n"]
    assert chat_settings.number_of_responses == 2
    assert chat_settings.logit_bias == {"1": 1}


def test_create_options():
    settings = OpenAIChatRequestSettings(
        temperature=0.5,
        top_p=0.5,
        presence_penalty=0.5,
        frequency_penalty=0.5,
        max_tokens=128,
        stop=["\n"],
        number_of_responses=2,
        logit_bias={"1": 1},
        messages=[{"role": "system", "content": "Hello"}],
    )
    options = settings.prepare_settings_dict()
    assert options["temperature"] == 0.5
    assert options["top_p"] == 0.5
    assert options["presence_penalty"] == 0.5
    assert options["frequency_penalty"] == 0.5
    assert options["max_tokens"] == 128
    assert options["stop"] == ["\n"]
    assert options["n"] == 2
    assert options["logit_bias"] == {"1": 1}
    assert not options["stream"]


def test_create_options_azure_data():
    az_source = AzureAISearchDataSources(
        indexName="test-index", endpoint="test-endpoint", key="test-key"
    )
    settings = AzureOpenAIChatRequestSettings(data_sources=[az_source])
    print(settings.model_dump(exclude_none=True, by_alias=True))
    assert False


def test_azure_open_ai_chat_request_settings_with_data_sources():  # noqa: E501
    input_dict = {
        "messages": [{'role': 'system', 'content': 'Hello'}],
        "extra_body": {
            "dataSources": [
                {
                    "type": "AzureCosmosDB",
                    "parameters": {
                        "authentication": {
                            "type": "ConnectionString",
                            "connectionString": "mongodb+srv://onyourdatatest:{password}$@{cluster-name}.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000",
                        },
                        "databaseName": "vectordb",
                        "containerName": "azuredocs",
                        "indexName": "azuredocindex",
                        "embeddingDependency": {
                            "type": "DeploymentName",
                            "deploymentName": "{embedding deployment name}",
                        },
                        "fieldsMapping": {"vectorFields": ["contentvector"]},
                    },
                }
            ]
        }
    }
    settings = AzureOpenAIChatRequestSettings.model_validate(input_dict, strict=True, from_attributes=True)
    print(settings)
    print(f"{settings.extra_body=}")
    print(f"{type(settings.extra_body)=}")
    assert settings.extra_body.data_sources[0].type == "AzureCosmosDB"
