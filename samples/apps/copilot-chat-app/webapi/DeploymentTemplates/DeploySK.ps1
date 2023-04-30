<#
.SYNOPSIS
Creates a Semantic Kernel service deployment.
#>

param(
    [Parameter(Mandatory)]
    [string]
    # Name for the deployment
    $DeploymentName,

    [Parameter(Mandatory)]
    [string]
    # Subscriptiom to which to make the deployment
    $Subscription,

    [string]
    # Resource group to which to make the deployment
    $ResourceGroup = "",

    [string]
    # Region to which to make the deployment (ignored when deploying to an existing resource group)
    $Region = "South Central US",

    [string]
    # Package to deploy to web service
    $PackageUri = 'https://skaasdeploy.blob.core.windows.net/api/skaas.zip',

    [string]
    # SKU for the Azure App Service plan
    $AppServiceSku = "B1",

    [switch]
    # Switches on verbose template deployment output
    $DebugDeployment
)

$ErrorActionPreference = "Stop"

$templateFile = "$($PSScriptRoot)/sk.bicep"

if (!$ResourceGroup)
{
    $ResourceGroup = $DeploymentName + "-rg"
}

Write-Host "Log into your Azure account"
az login | out-null

az account set -s $Subscription

Write-Host "Creating resource group $($ResourceGroup) if it doesn't exist..."
az group create --location $Region --name $ResourceGroup --tags Creator=$env:UserName
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

Write-Host "Validating template file..."
az deployment group validate --name $DeploymentName --resource-group $ResourceGroup --template-file $templateFile --parameters name=$DeploymentName packageUri=$PackageUri appServiceSku=$AppServiceSku
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Deploying..."
if ($DebugDeployment) {
    az deployment group create --name $DeploymentName --resource-group $ResourceGroup --template-file $templateFile --debug --parameters name=$DeploymentName packageUri=$PackageUri appServiceSku=$AppServiceSku
}
else {
    az deployment group create --name $DeploymentName --resource-group $ResourceGroup --template-file $templateFile --parameters name=$DeploymentName packageUri=$PackageUri appServiceSku=$AppServiceSku
}