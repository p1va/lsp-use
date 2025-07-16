using StreamJsonRpc;

#pragma warning disable 


namespace LspUse.TestHarness.TestSources;

public class JsonRpcTest
{
    public void M()
    {
        var rpc = new JsonRpc(new MemoryStream());
    }
}

#pragma warning restore
