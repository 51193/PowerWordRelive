namespace PowerWordRelive.Infrastructure.Data;

public interface IIncrementalContainer<T>
{
    void Add(int index, T content);
    void Add(T content);
    void Remove(int index);
    void Edit(int index, T newContent);
    IReadOnlyList<T> Get(int count);
}