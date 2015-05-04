﻿using System;
using System.Collections.Generic;
using System.Linq;
using Crux.Core.Extensions;
using Crux.Domain.Entities;
using Crux.Domain.Persistence;
using Crux.Domain.Persistence.NHibernate;
using NHibernate;
using NUnit.Framework;

namespace Crux.Domain.Testing.Persistence
{
    public abstract class DomainPersistenceTester : DomainPersistenceTester<int> { }

    [Category("PersistenceTest")]
    public abstract class DomainPersistenceTester<TId>
    {
        private IList<object> _insertedEntities;

        public abstract ISessionFactory SessionFactory { get; }
        public abstract IDbConnectionProvider ConnectionProvider { get; }

        protected INHibernateUnitOfWork UnitOfWork { get; private set; }
        protected IRepositoryOfId<TId> Repository { get; private set; }
        protected bool ReverseTearDown { get; set; }

        [SetUp]
        public void SetUp()
        {
            UnitOfWork = new NHibernateUnitOfWork(SessionFactory, ConnectionProvider);
            _insertedEntities = new List<object>();
            Repository = new NHibernateRepositoryOfId<TId>(UnitOfWork);
            ReverseTearDown = true;

            UnitOfWork.Start();
        }

        [TearDown]
        public void TearDown()
        {
            if (UnitOfWork == null || _insertedEntities == null) return;

            var entities = ReverseTearDown
                ? _insertedEntities.Reverse()
                : _insertedEntities;

            entities.Each(e => {
                // ReSharper disable AccessToDisposedClosure
                UnitOfWork.CurrentSession.Delete(e);
                UnitOfWork.Flush();
                UnitOfWork.Clear();
                // ReSharper restore AccessToDisposedClosure
            });

            UnitOfWork.Dispose();
        }

        protected void SaveSupportingEntity<T>(T entity) where T : DomainEntityOfId<TId>
        {
            Repository.Save(entity);
            UnitOfWork.Flush();

            _insertedEntities.Add(entity);
        }

        protected internal void SaveSupportingEntityOfDifferentIdType(object entity)
        {
            UnitOfWork.CurrentSession.Save(entity);
            UnitOfWork.Flush();

            _insertedEntities.Add(entity);
        }

        protected T VerifyPersistence<T>(T first) where T : DomainEntityOfId<TId>
        {
            var second = VerifyPersistence(first, Repository);
            _insertedEntities.Add(second);

            return second;
        }

        protected T VerifyPersistence<T, TEntityId>(T first, IRepositoryOfId<TEntityId> repository) where T : DomainEntityOfId<TEntityId>
        {
            repository.Save(first);
            UnitOfWork.Flush();
            UnitOfWork.Clear();

            return repository.Load<T>(first.ID);
        }

        protected void SetDeleteOrder(IList<DomainEntityOfId<TId>> entites)
        {
            ReverseTearDown = false;
            _insertedEntities = entites
                .Cast<object>()
                .ToList();
        }

        protected void RunInUnitOfWork(Action<IRepositoryOfId<TId>> action)
        {
            var unitOfWork = new NHibernateUnitOfWork(SessionFactory, ConnectionProvider);
            var repository = new NHibernateRepositoryOfId<TId>(unitOfWork);

            using (var scope = unitOfWork.CreateScope()) {
                action.Invoke(repository);
                scope.Complete();
            }
        }
    }
}
