// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Extensions;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using NUnit.Framework;

namespace MediaPi.Core.Tests.RestModels;

[TestFixture]
public class CategoryDtoTests
{
    [Test]
    public void CategoryCreateItem_DefaultsFreeToTrue()
    {
        var dto = new CategoryCreateItem { Title = "Movies" };

        Assert.That(dto.Free, Is.True);
        Assert.That(dto.ToString(), Does.Contain("Movies"));
    }

    [Test]
    public void CategoryViewItem_CopiesCategoryFields()
    {
        var category = new Category { Id = 7, Title = "Sport", Free = false };

        var dto = category.ToViewItem();

        Assert.That(dto.Id, Is.EqualTo(7));
        Assert.That(dto.Title, Is.EqualTo("Sport"));
        Assert.That(dto.Free, Is.False);
        Assert.That(dto.ToString(), Does.Contain("Sport"));
    }

    [Test]
    public void CategoryUpdateItem_NullValuesDoNotChangeCategory()
    {
        var category = new Category { Id = 1, Title = "Existing", Free = true };

        category.UpdateFrom(new CategoryUpdateItem { Title = null, Free = null });

        Assert.That(category.Title, Is.EqualTo("Existing"));
        Assert.That(category.Free, Is.True);
    }

    [Test]
    public void CategoryUpdateItem_ProvidedValuesUpdateCategory()
    {
        var category = new Category { Id = 1, Title = "Existing", Free = true };

        category.UpdateFrom(new CategoryUpdateItem { Title = "Updated", Free = false });

        Assert.That(category.Title, Is.EqualTo("Updated"));
        Assert.That(category.Free, Is.False);
    }

    [Test]
    public void CategoryUpdateItem_ToStringSerializesValues()
    {
        var dto = new CategoryUpdateItem { Title = "Updated", Free = false };

        Assert.That(dto.ToString(), Does.Contain("Updated"));
    }
}
