 = "c:\Users\Dell\Desktop\ManagePetStore\ManagePetStore"
 = @("Admin", "Cashier", "Customer", "Manager", "Warehouse")

function MoveAndRenameNamespace(, , , ) {
    if (Test-Path ) {
        if (-not (Test-Path )) { New-Item -ItemType Directory -Path  | Out-Null }
         = Get-ChildItem -Path  -Recurse -File -Filter "*.cs"
        foreach ( in ) {
             = Get-Content .FullName -Raw
             =  -replace "namespace ", "namespace "
            Set-Content -Path .FullName -Value  -Encoding UTF8
            
             = Join-Path  .Name
            Move-Item -Path .FullName -Destination  -Force
        }
        Write-Host "Moved files from  to "
    }
}

foreach ( in ) {
    MoveAndRenameNamespace (Join-Path  "Areas\\Models") (Join-Path  "Models\") "ManagePetStore.Areas..Models" "ManagePetStore.Models."
    MoveAndRenameNamespace (Join-Path  "Areas\\Services") (Join-Path  "Services\") "ManagePetStore.Areas..Services" "ManagePetStore.Services."
    MoveAndRenameNamespace (Join-Path  "Areas\\Repositories") (Join-Path  "Repositories\") "ManagePetStore.Areas..Repositories" "ManagePetStore.Repositories."
}

# SpaServices -> ServiceStaff
MoveAndRenameNamespace (Join-Path  "SpaServices\Models") (Join-Path  "Models\ServiceStaff") "ManagePetStore.SpaServices.Models" "ManagePetStore.Models.ServiceStaff"

Write-Host "Done phase 1"
