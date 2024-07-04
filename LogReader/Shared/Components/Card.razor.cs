using Microsoft.AspNetCore.Components;

namespace LogReader.Shared.Components;

public partial class Card
{
    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public bool InitialCollapsed { get; set; }

    [Parameter]
    public ClosableType Closable { get; set; } = ClosableType.Collapse;

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public RenderFragment? ExtraButtonsFragment { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public EventCallback OnCloseClicked { get; set; }

    [Parameter]
    public EventCallback<bool> OnToggleCollapse { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object?>? AdditionalAttributes { get; set; }

    protected bool Collapsed;

    protected override void OnInitialized() => Collapsed = Closable == ClosableType.Collapse && InitialCollapsed;

    protected virtual Task HeaderClicked()
    {
        if (Closable != ClosableType.Collapse)
            return Task.CompletedTask;

        Collapsed = !Collapsed;

        if (OnToggleCollapse.HasDelegate)
            OnToggleCollapse.InvokeAsync(Collapsed);

        return Task.CompletedTask;
    }

    private void CloseClicked()
    {
        if (Closable == ClosableType.Close && OnCloseClicked.HasDelegate)
            OnCloseClicked.InvokeAsync();
    }

    public enum ClosableType
    {
        None,
        Collapse,
        Close
    }
}