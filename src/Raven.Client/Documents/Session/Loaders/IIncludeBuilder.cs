using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Raven.Client.Documents.Conventions;
using Raven.Client.Extensions;

namespace Raven.Client.Documents.Session.Loaders
{
    public interface IIncludeOperations<T>
    {
        IIncludeOperations<T> IncludeDocuments(string path);

        IIncludeOperations<T> IncludeDocuments(Expression<Func<T, string>> path);

        IIncludeOperations<T> IncludeDocuments(Expression<Func<T, IEnumerable<string>>> path);

        IIncludeOperations<T> IncludeDocuments<TInclude>(Expression<Func<T, string>> path);

        IIncludeOperations<T> IncludeDocuments<TInclude>(Expression<Func<T, IEnumerable<string>>> path);

        IIncludeOperations<T> IncludeCounter(string name);

        IIncludeOperations<T> IncludeCounters(string[] names);

        IIncludeOperations<T> IncludeAllCounters();

        IIncludeOperations<T> IncludeCounter(Expression<Func<T, string>> path, string name);

        IIncludeOperations<T> IncludeCounters(Expression<Func<T, string>> path, string[] names);

        IIncludeOperations<T> IncludeAllCounters(Expression<Func<T, string>> path);
    }

    public interface IIncludeBuilder<T>
    {
        IIncludeOperations<T> IncludeDocuments(string path);

        IIncludeOperations<T> IncludeDocuments(Expression<Func<T, string>> path);

        IIncludeOperations<T> IncludeDocuments(Expression<Func<T, IEnumerable<string>>> path);

        IIncludeOperations<T> IncludeDocuments<TInclude>(Expression<Func<T, string>> path);

        IIncludeOperations<T> IncludeDocuments<TInclude>(Expression<Func<T, IEnumerable<string>>> path);

        IIncludeOperations<T> IncludeCounter(string name);

        IIncludeOperations<T> IncludeCounters(string[] names);

        IIncludeOperations<T> IncludeAllCounters();
    }


    public interface IQueryIncludeBuilder<T> : IIncludeBuilder<T>
    {
        IIncludeOperations<T> IncludeCounter(Expression<Func<T, string>> path, string name);

        IIncludeOperations<T> IncludeCounters(Expression<Func<T, string>> path, string[] names);

        IIncludeOperations<T> IncludeAllCounters(Expression<Func<T, string>> path);
    }

    public class IncludeBuilder
    {
        internal HashSet<string> DocumentsToInclude;
        internal HashSet<string> CountersToInclude => CountersToIncludeBySourcePath[string.Empty].CountersToInclude;
        internal bool AllCounters => CountersToIncludeBySourcePath[string.Empty].AllCounters;
        internal string Alias;

        internal Dictionary<string, (bool AllCounters, HashSet<string> CountersToInclude)> CountersToIncludeBySourcePath;
    }

    internal class IncludeBuilder<T> : IncludeBuilder, IQueryIncludeBuilder<T>, IIncludeOperations<T>
    {
        private readonly DocumentConventions _conventions;

        internal IncludeBuilder(DocumentConventions conventions)
        {
            _conventions = conventions;
        }

        public IIncludeOperations<T> IncludeDocuments(string path)
        {
            if (DocumentsToInclude == null)
                DocumentsToInclude = new HashSet<string>();
            DocumentsToInclude.Add(path);
            return this;
        }

        public IIncludeOperations<T> IncludeDocuments(Expression<Func<T, string>> path)
        {
            return IncludeDocuments(path.ToPropertyPath());
        }

        public IIncludeOperations<T> IncludeDocuments(Expression<Func<T, IEnumerable<string>>> path)
        {
            return IncludeDocuments(path.ToPropertyPath());
        }


        public IIncludeOperations<T> IncludeDocuments<TInclude>(Expression<Func<T, string>> path)
        {
            return IncludeDocuments(IncludesUtil.GetPrefixedIncludePath<TInclude>(path.ToPropertyPath(), _conventions));
        }

        public IIncludeOperations<T> IncludeDocuments<TInclude>(Expression<Func<T, IEnumerable<string>>> path)
        {
            return IncludeDocuments(IncludesUtil.GetPrefixedIncludePath<TInclude>(path.ToPropertyPath(), _conventions));
        }

        public IIncludeOperations<T> IncludeCounter(string name)
        {
            IncludeCounter(string.Empty, name);
            return this;
        }


        public IIncludeOperations<T> IncludeCounter(Expression<Func<T, string>> path, string name)
        {
            WithAlias(path);
            IncludeCounter(path.ToPropertyPath(), name);
            return this;
        }

        public IIncludeOperations<T> IncludeCounters(string[] names)
        {
            IncludeCounters(string.Empty, names);
            return this;
        }

        public IIncludeOperations<T> IncludeCounters(Expression<Func<T, string>> path, string[] names)
        {
            WithAlias(path);
            IncludeCounters(path.ToPropertyPath(), names);
            return this;
        }

        public IIncludeOperations<T> IncludeAllCounters()
        {
            IncludeAll(string.Empty);
            return this;
        }

        public IIncludeOperations<T> IncludeAllCounters(Expression<Func<T, string>> path)
        {
            WithAlias(path);
            IncludeAll(path.ToPropertyPath());
            return this;
        }

        private void IncludeCounter(string path, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            AssertNotAllAndAddNewEntryIfNeeded(path);

            CountersToIncludeBySourcePath[path]
                .CountersToInclude.Add(name);
        }

        private void IncludeCounters(string path, string[] names)
        {
            if (names == null)
                throw new ArgumentNullException(nameof(names));

            AssertNotAllAndAddNewEntryIfNeeded(path);

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidOperationException("Counters(string[] names) : 'names' should not " +
                                                        "contain null or whitespace elements");
                CountersToIncludeBySourcePath[path]
                    .CountersToInclude.Add(name);
            }

        }

        private void IncludeAll(string sourcePath)
        {
            if (CountersToIncludeBySourcePath == null)
            {
                CountersToIncludeBySourcePath = new Dictionary<string,
                    (bool, HashSet<string>)>(StringComparer.OrdinalIgnoreCase);
            }

            if (CountersToIncludeBySourcePath.TryGetValue(sourcePath, out var val) &&
                val.CountersToInclude != null)

                throw new InvalidOperationException("IIncludeBuilder : You cannot use AllCounters() after using " +
                                                    "Counter(string name) or Counters(string[] names)");

            CountersToIncludeBySourcePath[sourcePath] = (true, null);
        }

        private void AssertNotAllAndAddNewEntryIfNeeded(string path)
        {
            if (CountersToIncludeBySourcePath != null &&
                CountersToIncludeBySourcePath.TryGetValue(path, out var val) &&
                val.AllCounters)
                throw new InvalidOperationException("IIncludeBuilder : You cannot use Counter(name) after using AllCounters() ");

            if (CountersToIncludeBySourcePath == null)
            {
                CountersToIncludeBySourcePath = new Dictionary<string,
                    (bool, HashSet<string>)>(StringComparer.OrdinalIgnoreCase);
            }

            if (CountersToIncludeBySourcePath.ContainsKey(path) == false)
            {
                CountersToIncludeBySourcePath[path] = (false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }

        private void WithAlias(Expression<Func<T, string>> path)
        {
            if (Alias == null)
                Alias = path.Parameters[0].Name;
        }

    }

}