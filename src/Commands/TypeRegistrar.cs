using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

sealed class TypeRegistrar(IServiceCollection? builder = default) : ITypeRegistrar
{
    readonly IServiceCollection builder = builder ?? new ServiceCollection();

    public IServiceCollection Services => builder;

    public ITypeResolver Build() => new TypeResolver(builder.BuildServiceProvider());

    public void Register(Type service, Type implementation) => builder.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) => builder.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> func)
    {
        ThrowIfNull(func);
        builder.AddSingleton(service, (provider) => func());
    }

    sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
    {
        readonly IServiceProvider provider = provider ?? throw new ArgumentNullException(nameof(provider));

        public object? Resolve(Type? type) => type == null ? null : provider.GetService(type);

        public void Dispose() => (provider as IDisposable)?.Dispose();
    }
}