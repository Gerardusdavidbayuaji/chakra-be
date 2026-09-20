param(
    [Parameter(Mandatory=$true)]
    [string]$Name
)

dotnet ef migrations add $Name --project Chakra.Infrastructure --startup-project Chakra.API
