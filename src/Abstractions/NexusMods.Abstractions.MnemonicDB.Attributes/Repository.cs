using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.MnemonicDB.Abstractions.Models;

namespace NexusMods.Abstractions.MnemonicDB.Attributes;

internal class Repository<TModel> : IRepository<TModel> where TModel : Entity
{
    private readonly IConnection _conn;
    private readonly IAttribute[] _watchedAttributes;

    /// <summary>
    /// DI constructor.
    /// </summary>
    public Repository(IAttribute[] watchedAttributes, IConnection connection)
    {
        _conn = connection;
        _watchedAttributes = watchedAttributes;
        
        _cache
            .Connect()
            .Bind(out _observable)
            .Subscribe();
        
        _conn.Revisions
            .SelectMany(db => db.Datoms(db.BasisTxId).Select(datom => (datom, db)))
            .Where(d => _watchedAttributes.Contains(d.datom.A))
            .Select(d => (Db: d.db, E: d.datom.E))
            .StartWith(All.Select(l => (Db: l.Db, E:l.Id)))
            .Subscribe(dbAndE =>
            {
                _cache.Edit(x =>
                {
                    x.AddOrUpdate(dbAndE.Db.Get<TModel>(dbAndE.E));
                });
            });
    }

    /// <inheritdoc />
    public IEnumerable<TModel> All
    {
        get
        {
            var db = _conn.Db;
            var items = db.Find(_watchedAttributes[0]);
            foreach (var attr in _watchedAttributes.Skip(1))
            {
                items = items.Intersect(db.Find(attr));
            }

            return items.Select(id => db.Get<TModel>(id));
        }
    }
    
    private readonly SourceCache<TModel, EntityId> _cache = new(x => x.Id);

    private readonly ReadOnlyObservableCollection<TModel> _observable;

    /// <inheritdoc />
    public ReadOnlyObservableCollection<TModel> Observable => _observable;

    /// <inheritdoc />
    public IObservable<TModel> Revisions(EntityId id)
    {
        return _conn.Revisions
            .Where(db => db.Datoms(db.BasisTxId).Any(datom => datom.E == id.Value))
            .StartWith(_conn.Db)
            .Select(db => db.Get<TModel>(id))
            .Where(IsValid);
    }

    private bool IsValid(TModel model)
    {
        foreach (var attribute in _watchedAttributes)
        {
            if (!model.Contains(attribute))
                return false;
        }
        return true;
    }

    /// <inheritdoc />
    public bool Exists(EntityId eid)
    {
        var entity = _conn.Db.Get<TModel>(eid);
        foreach (var attribute in _watchedAttributes)
        {
            if (!entity.Contains(attribute))
                return false;
        }

        return true;
    }

    public async Task Delete(TModel model)
    {
        var tx = _conn.BeginTransaction();
        // For each attribute, resolve it to a IAttribute (default is A = ushort), then
        // use that to call .Add with isRetract = true and pass in the value by object
        foreach (var attr in model.Select(d => d.Resolved))
        {
            // This does a bunch of casting, and isn't optimal, but it's such a rarely used usecase
            // it's fine for now. We can optimize this later by adding methods to `Datom` and `IReadDatom`
            // in MnemonicDB
            attr.A.Add(tx, model.Id, attr.ObjectValue, true);
        }

        await tx.Commit();
    }

    public bool TryFindFirst<TOuter, TInner>(Attribute<TOuter, TInner> attr, TOuter value, [NotNullWhen(true)] out TModel? model)
    {
        Debug.Assert(attr.IsIndexed, "Attribute must be indexed to be used in a find operation");
        var db = _conn.Db;
        var items = db.FindIndexed(value, attr);
        foreach (var item in items)
        {
            var entity = db.Get<TModel>(item);
            if (IsValid(entity))
            {
                model = entity;
                return true;
            }
        }

        model = null;
        return false;
    }

    public bool TryFindFirst([NotNullWhen(true)] out TModel? model)
    {
        foreach (var item in All)
        {
            model = item;
            return true;
        }
        model = null;
        return false;
    }

    public IEnumerable<TModel> FindAll<TOuter, TInner>(Attribute<TOuter, TInner> attr, TOuter value)
    {
        Debug.Assert(attr.IsIndexed, "Attribute must be indexed to be used in a find operation");
        var db = _conn.Db;
        var items = db.FindIndexed(value, attr);
        foreach (var item in items)
        {
            var entity = db.Get<TModel>(item);
            if (IsValid(entity))
            {
                yield return entity;
            }
        }
    }
}


/// <summary>
/// DI extensions for the repository.
/// </summary>
public static class ServiceExtensions
{
    
    /// <summary>
    /// Registers a repository for a model with the given attributes.
    /// </summary>
    public static IServiceCollection AddRepository<TModel>(this IServiceCollection collection, params IAttribute[] attributes) where TModel : Entity
    {
        if (attributes.Length == 0)
            throw new InvalidOperationException("At least one attribute must be provided when creating a repository");
        return collection
            .AddSingleton<IRepository<TModel>>(provider => 
                new Repository<TModel>(attributes, provider.GetRequiredService<IConnection>()));
    }
}