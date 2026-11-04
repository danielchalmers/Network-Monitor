using System.Collections.Generic;
using System.Threading.Tasks;
using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class RouterCheckTests
{
    private const string Router = "192.168.1.1";

    /// <summary>
    /// Pings that finish when told to, so each test decides how the router answers.
    /// </summary>
    private readonly Queue<TaskCompletionSource<bool>> _pings = new();

    private RouterCheck CreateCheck() => new(_ =>
    {
        var ping = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pings.Enqueue(ping);
        return ping.Task;
    });

    private async Task AnswerAsync(bool answered)
    {
        _pings.Dequeue().SetResult(answered);

        // The reply is noted on another thread.
        await Task.Delay(50);
    }

    [Fact]
    public void Check_WithNoRouter_SaysNothing()
    {
        var check = CreateCheck();

        Assert.Null(check.Check(null));
        Assert.Empty(_pings);
    }

    [Fact]
    public void Check_BeforeTheFirstReply_SaysItsChecking()
    {
        var check = CreateCheck();

        Assert.Equal("Checking your router…", check.Check(Router)?.Note);
    }

    [Fact]
    public async Task Check_WhenTheRouterAnswers_SaysTheProblemIsPastIt()
    {
        var check = CreateCheck();

        check.Check(Router);
        await AnswerAsync(true);

        Assert.Equal("Your router is fine; the problem is past it", check.Check(Router)?.Note);
    }

    [Fact]
    public async Task Check_WhenTheRouterDoesntAnswer_SaysSo()
    {
        var check = CreateCheck();

        check.Check(Router);
        await AnswerAsync(false);

        Assert.Equal("Your router isn't answering", check.Check(Router)?.Note);
    }

    [Fact]
    public void Check_WhileAPingIsWaiting_DoesntSendAnother()
    {
        var check = CreateCheck();

        check.Check(Router);
        check.Check(Router);
        check.Check(Router);

        Assert.Single(_pings);
    }
}
