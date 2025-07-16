namespace LspUse.TestHarness.TestSources;

public class DiagnosticsErrorTest
{
    public class Test
    {
        public Test()
        {

        }

        public Test(string msg)
        {
            Message = msg ?? throw new ArgumentNullException(nameof(msg));
        }

        public Test(string msg, string fun)
        {
            Message = msg ?? throw new ArgumentNullException(nameof(msg));
            Fun = fun ?? throw new ArgumentNullException(nameof(fun));
        }

        [Obsolete("Don't use me")]
        public string? Message { get; set; }

        public string Fun { get; set; }
    }

    public static void M()
    {
        var test = new Test(null);
        _ = test.Message.ToString(); // This should warn about obsolete usage
        test.Fun = "Fun";

        // Add some actual errors
        // UndefinedMethod(); // Error: method doesn't exist
        // int x = "string"; // Error: cannot convert string to int
    }

    public static void M2()
    {
        _ = new Test
        {
            Fun = "X"
        };
    }
}
