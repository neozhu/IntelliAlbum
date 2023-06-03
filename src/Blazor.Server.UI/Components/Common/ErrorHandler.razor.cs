namespace Blazor.Server.UI.Components.Common;

public partial class ErrorHandler
{

    public List<Exception> ReceivedExceptions = new();

    protected override  Task OnErrorAsync(Exception exception)
    {
        ReceivedExceptions.Add(exception);
        switch (exception)
        {
            case UnauthorizedAccessException:
                Snackbar.Add("Authentication Failed", Severity.Error);
                break;
        }
        return Task.CompletedTask;
    }

    public new void Recover()
    {
        ReceivedExceptions.Clear();
        base.Recover();
    }
}