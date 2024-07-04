using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;

namespace LogReader;

public partial class MainForm : Form
{
    public MainForm(IServiceProvider provider)
    {
        InitializeComponent();

        _view.HostPage = "wwwroot\\index.html";
        _view.Services = provider;
        _view.RootComponents.Add<App>("#app");
        _view.UrlLoading += (_, args) =>
        {
            if (args.Url.Host != "0.0.0.0")
                args.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
        };
    }
}