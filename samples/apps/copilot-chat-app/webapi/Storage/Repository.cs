﻿namespace SKWebApi.Storage;

/// <summary>
/// Defines the basic CRUD operations for a repository.
/// </summary>
public class Repository<T> : IRepository<T> where T : IStorageEntity
{
    /// <summary>
    /// The storage context.
    /// </summary>
    protected IStorageContext<T> _StorageContext;

    /// <summary>
    /// Initializes a new instance of the Repository class.
    /// </summary>
    public Repository(IStorageContext<T> storageContext)
    {
        this._StorageContext = storageContext;
    }

    /// <inheritdoc/>
    public Task CreateAsync(T entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            throw new ArgumentOutOfRangeException("Invalid id.");
        }

        return this._StorageContext.CreateAsync(entity);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(T entity)
    {
        return this._StorageContext.DeleteAsync(entity);
    }

    /// <inheritdoc/>
    public Task<T> FindByIdAsync(string id)
    {
        return this._StorageContext.ReadAsync(id);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(T entity)
    {
        return this._StorageContext.UpdateAsync(entity);
    }
}
