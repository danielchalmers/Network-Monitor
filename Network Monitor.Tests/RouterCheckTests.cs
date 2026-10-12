using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class RouterCheckTests
{
    private static readonly NetworkId Home = new("wifi", "192.168.1.1", "AA-BB-CC-00-11-22");
    private static readonly NetworkId Cafe = new("wifi", "192.168.1.1", "DD-EE-FF-33-44-55");

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
    public void Describe_WithNoRouter_SaysNothing()
    {
        var check = CreateCheck();

        check.Check(null);

        Assert.Null(check.Describe());
        Assert.Empty(_pings);
    }

    [Fact]
    public void Describe_BeforeTheFirstReply_SaysItsChecking()
    {
        var check = CreateCheck();

        check.Check(Home);

        Assert.Equal("Checking your router…", check.Describe()?.Note);
    }

    [Fact]
    public async Task Describe_WhenTheRouterAnswers_SaysTheProblemIsPastIt()
    {
        var check = CreateCheck();

        check.Check(Home);
        await AnswerAsync(true);

        Assert.Equal("Your router is fine; the problem is past it", check.Describe()?.Note);
    }

    [Fact]
    public async Task Describe_ARouterThatHasNeverAnswered_IsntBlamed()
    {
        var check = CreateCheck();

        check.Check(Home);
        await AnswerAsync(false);

        Assert.Equal("Can't tell if it's your router; it hasn't answered a ping yet", check.Describe()?.Note);
    }

    [Fact]
    public async Task Describe_ARouterThatAnsweredBefore_IsBlamed()
    {
        var check = CreateCheck();
        check.Check(Home);
        await AnswerAsync(true);
        check.Stop();

        check.Check(Home);
        await AnswerAsync(false);

        Assert.Equal("Your router isn't answering", check.Describe()?.Note);
    }

    [Fact]
    public async Task Describe_AnotherNetworksRouterWithTheSameAddress_IsntBlamed()
    {
        var check = CreateCheck();
        check.Check(Home);
        await AnswerAsync(true);
        check.Stop();

        check.Check(Cafe);
        await AnswerAsync(false);

        Assert.Equal("Can't tell if it's your router; it hasn't answered a ping yet", check.Describe()?.Note);
    }

    [Fact]
    public void Check_WhileAPingIsWaiting_DoesntSendAnother()
    {
        var check = CreateCheck();

        check.Check(Home);
        check.Check(Home);
        check.Check(Home);

        Assert.Single(_pings);
    }

    [Fact]
    public async Task Stop_ForgetsThisOutage_SoAReplyFromItDoesntCountTowardTheNext()
    {
        var check = CreateCheck();
        check.Check(Home);
        check.Stop();

        await AnswerAsync(true);
        check.Check(Home);

        Assert.Equal("Checking your router…", check.Describe()?.Note);
    }
}
