﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Search;

/// <summary>
/// Enabled filtering when calling <see cref="ITextSearch{T}.SearchAsync"/>.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class FilterOptions
{
    /// <summary>
    /// The equals clauses to apply to the <see cref="FilterOptions" />
    /// </summary>
    public IEnumerable<FilterClause> FilterClauses => this._filterClauses;

    /// <summary>
    /// Add a equals clause to the filter options.
    /// </summary>
    /// <param name="field">Name of the field.</param>
    /// <param name="value">Value of the field</param>
    /// <returns>FilterOptions instance to allow fluent configuration.</returns>
    public FilterOptions Equals(string field, object value)
    {
        this._filterClauses.Add(new EqualityFilterClause(field, value));
        return this;
    }

    #region private
    private readonly List<FilterClause> _filterClauses = [];
    #endregion
}
