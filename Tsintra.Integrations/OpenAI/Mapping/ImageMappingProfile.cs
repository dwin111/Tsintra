using System;
using OpenAI.Images;
using Tsintra.App.Models;

namespace Tsintra.Integrations.OpenAI.Mapping
{
    public static class ImageMapping
    {
        public static GeneratedImageQuality Map(ImageQuality quality) => quality switch
        {
            ImageQuality.Standard => GeneratedImageQuality.Standard,
            ImageQuality.High => GeneratedImageQuality.High,
            _ => throw new ArgumentOutOfRangeException(
                nameof(quality), quality, "Непідтримувана якість зображення")
        };

        public static GeneratedImageSize Map(ImageSize size) => size switch
        {
            ImageSize.W256xH256 => GeneratedImageSize.W256xH256,
            ImageSize.W512xH512 => GeneratedImageSize.W512xH512,
            ImageSize.W1024xH1024 => GeneratedImageSize.W1024xH1024,
            _ => throw new ArgumentOutOfRangeException(
                nameof(size), size, "Непідтримуваний розмір зображення")
        };

        public static GeneratedImageStyle Map(ImageStyle style) => style switch
        {
            ImageStyle.Natural => GeneratedImageStyle.Natural,
            ImageStyle.Vivid => GeneratedImageStyle.Vivid,
            _ => throw new ArgumentOutOfRangeException(
                nameof(style), style, "Непідтримуваний стиль зображення")
        };

        public static GeneratedImageFormat Map(ImageFormat format) => format switch
        {
            ImageFormat.Uri => GeneratedImageFormat.Uri,
            ImageFormat.Bytes => GeneratedImageFormat.Bytes,
            _ => throw new ArgumentOutOfRangeException(
                nameof(format), format, "Непідтримуваний формат відповіді")
        };
    }
}