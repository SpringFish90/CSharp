class MyAsync
{
    static async Task Main(string[] args)
    {
        Task t5 = MyAsync.DoAsync(5);
        await MyAsync.DoAsync(3);
        MyAsync.DoAsync(2);
        //await t5;
    }
    public static async Task DoAsync(int num = 1)
    {
        for (int i = num; i > -1; i--)
        {
            await Task.Delay(1000);
            Console.WriteLine($"{num} {i}");
        }
    }
}
