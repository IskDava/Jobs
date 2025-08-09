class Variables
{
    public static Dictionary<string, object?> globals = new()
    {
        {"Running", true },

        // Paths
        {"AbsolutePath", null},
        {"RelativePath", null},
    };
}