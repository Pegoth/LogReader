using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace LogReader;

public partial class App
{
    public static event Func<KeyboardEventArgs, Task>? OnGlobalKeyup;

    [JSInvokable]
    public static async Task GlobalOnKeyup(KeyboardEventArgs e)
    {
        if (OnGlobalKeyup is null)
            return;

        await OnGlobalKeyup.Invoke(e);
    }
}