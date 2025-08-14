using System.Diagnostics.CodeAnalysis;

namespace DotNetMcp.Core.Common;

/// <summary>
/// Discriminated union for optional values, eliminating null reference issues
/// </summary>
public abstract record Option<T>
{
    public static Option<T> Some(T value) => new SomeOption<T>(value);
    public static Option<T> None() => new NoneOption<T>();
    
    public abstract bool IsSome { get; }
    public abstract bool IsNone { get; }
    
    // Pattern matching
    public abstract TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone);
    public abstract Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSome, Func<Task<TResult>> onNone);
    
    // Monadic operations
    public abstract Option<TNext> Map<TNext>(Func<T, TNext> mapper);
    public abstract Option<TNext> Bind<TNext>(Func<T, Option<TNext>> binder);
    public abstract Option<T> Filter(Func<T, bool> predicate);
    
    // Safe value access
    public abstract T GetValueOrDefault(T defaultValue);
    public abstract T GetValueOrDefault(Func<T> defaultFactory);
    
    public static implicit operator Option<T>(T? value) => 
        value is null ? None() : Some(value);
}

public sealed record SomeOption<T>(T Value) : Option<T>
{
    public override bool IsSome => true;
    public override bool IsNone => false;
    
    public override TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone) => onSome(Value);
    
    public override async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSome, Func<Task<TResult>> onNone) => 
        await onSome(Value);
    
    public override Option<TNext> Map<TNext>(Func<T, TNext> mapper) => Option<TNext>.Some(mapper(Value));
    
    public override Option<TNext> Bind<TNext>(Func<T, Option<TNext>> binder) => binder(Value);
    
    public override Option<T> Filter(Func<T, bool> predicate) => 
        predicate(Value) ? this : None();
    
    public override T GetValueOrDefault(T defaultValue) => Value;
    public override T GetValueOrDefault(Func<T> defaultFactory) => Value;
}

public sealed record NoneOption<T> : Option<T>
{
    public override bool IsSome => false;
    public override bool IsNone => true;
    
    public override TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone) => onNone();
    
    public override async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSome, Func<Task<TResult>> onNone) => 
        await onNone();
    
    public override Option<TNext> Map<TNext>(Func<T, TNext> mapper) => Option<TNext>.None();
    
    public override Option<TNext> Bind<TNext>(Func<T, Option<TNext>> binder) => Option<TNext>.None();
    
    public override Option<T> Filter(Func<T, bool> predicate) => this;
    
    public override T GetValueOrDefault(T defaultValue) => defaultValue;
    public override T GetValueOrDefault(Func<T> defaultFactory) => defaultFactory();
}

public static class Option
{
    public static Option<T> Some<T>(T value) => Option<T>.Some(value);
    public static Option<T> None<T>() => Option<T>.None();
    
    // Helper for nullable references
    public static Option<T> FromNullable<T>(T? value) where T : class => 
        value is null ? None<T>() : Some(value);
        
    // Helper for nullable value types  
    public static Option<T> FromNullable<T>(T? value) where T : struct =>
        value.HasValue ? Some(value.Value) : None<T>();
}