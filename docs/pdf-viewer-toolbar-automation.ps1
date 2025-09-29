param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath
)

Add-Type -AssemblyName UIAutomationClient

$process = Start-Process -FilePath $ExePath -PassThru
try {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $windowCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "PDF Viewer")
    $window = $null
    for ($i = 0; $i -lt 60 -and -not $window; $i++) {
        Start-Sleep -Milliseconds 250
        $window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $windowCondition)
    }

    if (-not $window) {
        throw "PDF Viewer window was not found."
    }

    function Invoke-AutomationButton {
        param([System.Windows.Automation.AutomationElement]$scope, [string]$automationId)
        $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
        $element = $scope.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if (-not $element) {
            throw "Automation element '$automationId' was not located."
        }

        $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $pattern.Invoke()
        Start-Sleep -Milliseconds 200
    }

    function Select-AutomationTab {
        param([System.Windows.Automation.AutomationElement]$scope, [string]$automationId)
        $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
        $tab = $scope.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if (-not $tab) {
            throw "Tab '$automationId' was not located."
        }

        $pattern = $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $pattern.Select()
        Start-Sleep -Milliseconds 200
    }

    Invoke-AutomationButton $window "ToolbarPagesButton"
    Invoke-AutomationButton $window "ToolbarZoomInButton"
    Invoke-AutomationButton $window "ToolbarZoomOutButton"
    Invoke-AutomationButton $window "AnnotationHighlightToggle"
    Invoke-AutomationButton $window "AnnotationUnderlineToggle"

    Select-AutomationTab $window "NavigationAnnotationsTab"
    Select-AutomationTab $window "NavigationOutlineTab"

    $tabControlCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "NavigationTabControl")
    $tabControl = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $tabControlCondition)
    if (-not $tabControl) {
        throw "NavigationTabControl was not found."
    }

    $selectionPattern = $tabControl.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
    $selected = $selectionPattern.Current.GetSelection()
    if ($selected.Length -eq 0 -or $selected[0].Current.AutomationId -ne "NavigationOutlineTab") {
        throw "Outline tab was not selected after automation interaction."
    }

    Write-Output "Toolbar and navigation interactions completed successfully."
}
finally {
    if ($process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(2000)) {
            $process.Kill()
        }
    }
}
