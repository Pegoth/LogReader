using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LogReader.Shared.Components;

public partial class Tooltip
{
    [Parameter]
    [EditorRequired]
    public string Text { get; set; } = null!;

    [Parameter]
    public bool EnableHtml { get; set; } = true;

    [Parameter]
    public string Placement { get; set; } = "top";

    [Parameter]
    public RenderFragment ChildContent { get; set; } = null!;

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool _initialized;

    [Inject]
    private IJSRuntime Runtime { get; set; } = null!;

    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized)
            return;

        await Runtime.InvokeVoidAsync("updateTooltips");
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await Runtime.InvokeVoidAsync("loadTooltips");
        _initialized = true;
    }
}