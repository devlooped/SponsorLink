namespace Devlooped;

static class AppDomainDictionary
{
    public static TValue Get<TValue>(string name) where TValue : notnull, new()
    {
        var data = AppDomain.CurrentDomain.GetData(name);
        if (data is TValue firstTry)
            return firstTry;

        lock (AppDomain.CurrentDomain)
        {
            if (AppDomain.CurrentDomain.GetData(name) is TValue secondTry)
                return secondTry;

            var newValue = new TValue();
            AppDomain.CurrentDomain.SetData(name, newValue);
            return newValue;
        }
    }
}
