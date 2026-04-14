// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public PaginationInfo Pagination { get; set; } = new();
    public SortingInfo Sorting { get; set; } = new();
    public string? Search { get; set; }
}
