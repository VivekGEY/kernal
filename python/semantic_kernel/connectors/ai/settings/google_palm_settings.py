# Copyright (c) Microsoft. All rights reserved.

from typing import Optional
from pydantic import SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict
from semantic_kernel.kernel_pydantic import HttpsUrl
from semantic_kernel.connectors.ai.open_ai.const import DEFAULT_AZURE_API_VERSION

class GooglePalmSettings(BaseSettings):
    """Google Palm model settings

    The settings are first loaded from environment variables with the prefix 'GOOGLE_PALM_'. If the environment variables
    are not found, the settings are loaded from a .env file with the encoding 'utf-8'. If the settings are not found in
    the .env file, the settings are ignored; however, validation will fail alerting that the settings are missing.

    Required settings for prefix 'GOOGLE_PALM_' are:
    - api_key: SecretStr - GooglePalm API key, see https://developers.generativeai.google/products/palm

    """
    model_config = SettingsConfigDict(env_prefix='GOOGLE_PALM', env_file='.env', env_file_encoding='utf-8', extra='ignore')

    api_key: SecretStr = None

