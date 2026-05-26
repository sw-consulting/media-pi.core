// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Data;

[TestFixture]
public class AppDbContextCategoryTests
{
    [Test]
    public void CategoryFree_HasDatabaseDefaultButIsSavedExplicitlyByEf()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"category_model_test_db_{Guid.NewGuid()}")
            .Options;

        using var db = new AppDbContext(options);

        var property = db.Model
            .FindEntityType(typeof(Category))!
            .FindProperty(nameof(Category.Free))!;

        Assert.That(property.GetDefaultValue(), Is.EqualTo(true));
        Assert.That(property.ValueGenerated, Is.EqualTo(ValueGenerated.Never));
    }
}
