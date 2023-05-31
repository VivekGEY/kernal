// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.util;

import java.io.IOException;

public class ClientSettings {
    /**
     * Returns an instance of OpenAISettings with key and organizationId from the environment
     *
     * @return OpenAISettings
     */
    public static OpenAISettings getOpenAISettingsFromEnv() {
        return new OpenAISettings().fromEnv();
    }

    /**
     * Returns an instance of AzureOpenAISettings with key, endpoint and deploymentName from the
     * environment
     *
     * @return AzureOpenAISettings
     */
    public static AzureOpenAISettings getAzureOpenAISettingsFromEnv() {
        return new AzureOpenAISettings().fromEnv();
    }

    /**
     * Returns an instance of OpenAISettings with key and organizationId from the properties file
     *
     * @param path Path to the properties file
     * @return OpenAISettings
     */
    public static OpenAISettings getOpenAISettingsFromFile(String path) throws IOException {
        return new OpenAISettings().fromFile(path);
    }

    /**
     * Returns an instance of OpenAISettings with key and organizationId from the properties file
     *
     * @param path Path to the properties file
     * @param clientSettingsId ID of the client settings in the properties file schema
     * @return OpenAISettings
     */
    public static OpenAISettings getOpenAISettingsFromFile(String path, String clientSettingsId)
            throws IOException {
        return new OpenAISettings().fromFile(path, clientSettingsId);
    }

    /**
     * Returns an instance of AzureOpenAISettings with key, endpoint and deploymentName from the
     * properties file
     *
     * @param path Path to the properties file
     * @return AzureOpenAISettings
     */
    public static AzureOpenAISettings getAzureOpenAISettingsFromFile(String path)
            throws IOException {
        return new AzureOpenAISettings().fromFile(path);
    }

    /**
     * Returns an instance of AzureOpenAISettings with key, endpoint and deploymentName from the
     * properties file
     *
     * @param path Path to the properties file
     * @param clientSettingsId ID of the client settings in the properties file schema
     * @return AzureOpenAISettings
     */
    public static AzureOpenAISettings getAzureOpenAISettingsFromFile(
            String path, String clientSettingsId) throws IOException {
        return new AzureOpenAISettings().fromFile(path, clientSettingsId);
    }
}
