/*
Copyright (c) Microsoft. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.

Bicep template for deploying Semantic Kernel to Azure as a web app service with an existing OpenAI account on openai.com.
*/

@description('Name for the deployment - Must consist of alphanumeric characters or \'-\'')
param name string = 'semkernel'

@description('SKU for the Azure App Service plan')
@allowed(['B1', 'S1', 'S2', 'S3', 'P1V3', 'P2V3', 'I1V2', 'I2V2' ])
param appServiceSku string = 'B1'

@description('Location of package to deploy as the web service')
#disable-next-line no-hardcoded-env-urls // This is an arbitrary package URI
param packageUri string = 'https://skaasdeploy.blob.core.windows.net/api/semantickernelapi.zip'

@description('Model to use for chat completions')
param completionModel string = 'gpt-3.5-turbo'

@description('Model to use for text embeddings')
param embeddingModel string = 'text-embedding-ada-002'

@description('Completion model the task planner should use')
param plannerModel string = 'gpt-3.5-turbo'

@secure()
@description('OpenAI API key')
param apiKey string = ''

@description('Whether to deploy Cosmos DB for chat storage')
param deployCosmosDB bool = true

@description('Whether to deploy Qdrant (in a container) for memory storage')
param deployQdrant bool = true

@description('Whether to deploy Azure Speech Services to be able to input chat text by voice')
param deploySpeechServices bool = true


module semanticKernel 'main.bicep' = {
  name: 'SemanticKernel'
  params: {
    name: name
    appServiceSku: appServiceSku
    packageUri: packageUri
    aiService: 'OpenAI'
    completionModel: completionModel
    embeddingModel: embeddingModel
    plannerModel: plannerModel
    endpoint: 'not-used'
    apiKey: apiKey
    deployCosmosDB: deployCosmosDB
    deployQdrant: deployQdrant
    deploySpeechServices: deploySpeechServices
    deployNewAzureOpenAI: false
  }
}


output endpoint string = semanticKernel.outputs.deployedUrl
