// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MediaPi.Core.RestModels;

public class VideoUploadItem
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public int AccountId { get; set; }

    [Required]
    public IFormFile? File { get; set; }
}
