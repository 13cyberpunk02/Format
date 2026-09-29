using Format.Print.Api.Cups;

namespace Format.Print.Tests;

public class IppTests
{
    [Fact]
    public void Written_request_can_be_parsed_back()
    {
        var bytes = new IppWriter(IppOperation.PrintJob, requestId: 42)
            .Group(IppTag.OperationGroup)
            .String(IppTag.Charset, "attributes-charset", "utf-8")
            .String(IppTag.Name, "job-name", "Чертёж 1")
            .Group(IppTag.JobGroup)
            .Integer("copies", 3)
            .String(IppTag.Keyword, "media", "Custom.914x1189mm")
            .Build();

        var parsed = IppResponse.Parse(bytes);

        Assert.Equal(IppOperation.PrintJob, parsed.StatusCode);
        Assert.Equal("utf-8", parsed.Get("attributes-charset"));
        Assert.Equal("Чертёж 1", parsed.Get("job-name"));
        Assert.Equal(3, parsed.Get("copies"));
        Assert.Equal("Custom.914x1189mm", parsed.Get("media"));
    }

    [Fact]
    public void Values_are_big_endian()
    {
        var bytes = new IppWriter(IppOperation.PrintJob, requestId: 1).Build();
        
        Assert.Equal(new byte[] { 2, 0, 0x00, 0x02 }, bytes[..4]);
    }
}