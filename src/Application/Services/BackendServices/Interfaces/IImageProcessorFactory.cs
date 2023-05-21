﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public interface IImageProcessorFactory
{
    IImageProcessor GetProcessor(string fileExtension);
    IHashProvider GetHashProvider();
    void SetContentPath(string contentPath);
}

/// <summary>
///     Interface representing a generic image processing pipeline. This
///     allows us to swap out different implementations etc depending on
///     performance and other characteristics.
/// </summary>
public interface IImageProcessor
{
    static ICollection<string> SupportedFileExtensions { get; }
    Task<IImageProcessResult> CreateThumbs(FileInfo source, IDictionary<FileInfo, IThumbConfig> destFiles);
    Task GetCroppedFile(FileInfo source, int x, int y, int width, int height, FileInfo destFile);
    Task CropImage(FileInfo path, int x, int y, int width, int height, Stream stream);
    Task TransformDownloadImage(string input, Stream output, IExportSettings exportConfig);
}
