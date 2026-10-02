using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgentWpf.Protocol;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Protocol.Tests;

[TestFixture]
public sealed class JsonLinesCodecTests
{
    [Test]
    public async Task Request_round_trips_through_one_line()
    {
        using var stream = new MemoryStream();
        var request = new DaemonRequest(["click", "@e3", "--input"], "C:\\work");

        await JsonLinesCodec.WriteAsync(stream, request);
        var text = Encoding.UTF8.GetString(stream.ToArray());
        stream.Position = 0;
        var read = await JsonLinesCodec.ReadRequestAsync(stream);

        text.Count(c => c == '\n').ShouldBe(1);
        text.ShouldEndWith("\n");
        read.ShouldNotBeNull();
        read.Args.ShouldBe(["click", "@e3", "--input"]);
        read.WorkingDirectory.ShouldBe("C:\\work");
        read.ProtocolVersion.ShouldBe(DaemonRequest.CurrentProtocolVersion);
    }

    [Test]
    public async Task Response_with_multiline_output_round_trips()
    {
        using var stream = new MemoryStream();
        var response = new DaemonResponse(3, "- window \"W\" [ref=e1]\n  - button \"OK\" [ref=e2]\n", "error: stale\nhint: run snapshot again\n");

        await JsonLinesCodec.WriteAsync(stream, response);
        stream.Position = 0;
        var read = await JsonLinesCodec.ReadResponseAsync(stream);

        read.ShouldBe(response);
    }

    [Test]
    public async Task End_of_stream_yields_null()
    {
        using var stream = new MemoryStream();

        (await JsonLinesCodec.ReadRequestAsync(stream)).ShouldBeNull();
    }

    [Test]
    public async Task Umlauts_survive_without_bom()
    {
        using var stream = new MemoryStream();

        await JsonLinesCodec.WriteAsync(stream, new DaemonRequest(["fill", "@e1", "Größe äöü"], "D:\\"));
        stream.ToArray()[0].ShouldBe((byte)'{');
        stream.Position = 0;

        (await JsonLinesCodec.ReadRequestAsync(stream))!.Args[2].ShouldBe("Größe äöü");
    }

    [Test]
    public void Pipe_name_is_scoped_to_user_and_session()
    {
        PipeNames.ForSession("ralf", "default").ShouldBe("agent-wpf-ralf-default");
        PipeNames.ForSession("Ralf Guder", "My Session").ShouldBe("agent-wpf-ralf_guder-my_session");
    }
}
