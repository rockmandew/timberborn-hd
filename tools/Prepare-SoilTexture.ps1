param(
    [string]$InputPath = (Join-Path $PSScriptRoot '..\assets\source\soil-neutral-seamless-source.png'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\mod\TimberbornHD\Textures\Soil'),
    [int]$Size = 2048
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class TimberbornHdTexturePrep
{
    public static void Run(string inputPath, string outputDirectory, int size)
    {
        Directory.CreateDirectory(outputDirectory);

        using (var input = new Bitmap(inputPath))
        using (var albedo = CreatePeriodicTile(input, size))
        {
            albedo.Save(Path.Combine(outputDirectory, "soil-neutral-albedo.png"), ImageFormat.Png);

            using (var normal = CreateNormalMap(albedo, 3.2f))
            {
                normal.Save(Path.Combine(outputDirectory, "soil-neutral-normal.png"), ImageFormat.Png);
            }

            using (var roughness = CreateRoughnessMap(albedo))
            {
                roughness.Save(Path.Combine(outputDirectory, "soil-neutral-roughness.png"), ImageFormat.Png);
            }
        }
    }

    private static Bitmap CreatePeriodicTile(Bitmap source, int size)
    {
        using (var resized = new Bitmap(size, size, PixelFormat.Format24bppRgb))
        {
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, 0, 0, size, size);
            }

            var output = new Bitmap(size, size, PixelFormat.Format24bppRgb);
            output.SetResolution(72, 72);
            BlendIntoPeriodicTile(resized, output);
            return output;
        }
    }

    private static void BlendIntoPeriodicTile(Bitmap source, Bitmap output)
    {
        var sourceRectangle = new Rectangle(0, 0, source.Width, source.Height);
        var outputRectangle = new Rectangle(0, 0, output.Width, output.Height);
        var sourceData = source.LockBits(sourceRectangle, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var outputData = output.LockBits(outputRectangle, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        try
        {
            unsafe
            {
                var sourcePointer = (byte*)sourceData.Scan0;
                var outputPointer = (byte*)outputData.Scan0;

                for (var y = 0; y < output.Height; y++)
                {
                    var offsetY = (y + output.Height / 2) % output.Height;
                    var normalizedY = y / (float)(output.Height - 1);
                    var sinY = (float)Math.Sin(Math.PI * normalizedY);
                    var weightY = sinY * sinY;

                    for (var x = 0; x < output.Width; x++)
                    {
                        var offsetX = (x + output.Width / 2) % output.Width;
                        var normalizedX = x / (float)(output.Width - 1);
                        var sinX = (float)Math.Sin(Math.PI * normalizedX);
                        var weightX = sinX * sinX;

                        var source00 = sourcePointer + y * sourceData.Stride + x * 3;
                        var source10 = sourcePointer + y * sourceData.Stride + offsetX * 3;
                        var source01 = sourcePointer + offsetY * sourceData.Stride + x * 3;
                        var source11 = sourcePointer + offsetY * sourceData.Stride + offsetX * 3;
                        var outputPixel = outputPointer + y * outputData.Stride + x * 3;

                        for (var channel = 0; channel < 3; channel++)
                        {
                            var top = source00[channel] * weightX + source10[channel] * (1f - weightX);
                            var bottom = source01[channel] * weightX + source11[channel] * (1f - weightX);
                            outputPixel[channel] = ToByte(top * weightY + bottom * (1f - weightY));
                        }
                    }
                }

                for (var y = 0; y < output.Height; y++)
                {
                    var first = outputPointer + y * outputData.Stride;
                    var last = outputPointer + y * outputData.Stride + (output.Width - 1) * 3;
                    last[0] = first[0];
                    last[1] = first[1];
                    last[2] = first[2];
                }

                for (var x = 0; x < output.Width; x++)
                {
                    var first = outputPointer + x * 3;
                    var last = outputPointer + (output.Height - 1) * outputData.Stride + x * 3;
                    last[0] = first[0];
                    last[1] = first[1];
                    last[2] = first[2];
                }
            }
        }
        finally
        {
            source.UnlockBits(sourceData);
            output.UnlockBits(outputData);
        }
    }

    private static Bitmap CreateNormalMap(Bitmap albedo, float strength)
    {
        var output = new Bitmap(albedo.Width, albedo.Height, PixelFormat.Format24bppRgb);
        var rectangle = new Rectangle(0, 0, albedo.Width, albedo.Height);
        var sourceData = albedo.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var outputData = output.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        try
        {
            unsafe
            {
                var sourcePointer = (byte*)sourceData.Scan0;
                var outputPointer = (byte*)outputData.Scan0;

                for (var y = 0; y < albedo.Height; y++)
                {
                    var up = (y - 1 + albedo.Height) % albedo.Height;
                    var down = (y + 1) % albedo.Height;

                    for (var x = 0; x < albedo.Width; x++)
                    {
                        var left = (x - 1 + albedo.Width) % albedo.Width;
                        var right = (x + 1) % albedo.Width;
                        var dx = Luminance(sourcePointer + y * sourceData.Stride + right * 3)
                                 - Luminance(sourcePointer + y * sourceData.Stride + left * 3);
                        var dy = Luminance(sourcePointer + down * sourceData.Stride + x * 3)
                                 - Luminance(sourcePointer + up * sourceData.Stride + x * 3);

                        var nx = -dx * strength / 255f;
                        var ny = -dy * strength / 255f;
                        var nz = 1f;
                        var inverseLength = 1f / (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        nx *= inverseLength;
                        ny *= inverseLength;
                        nz *= inverseLength;

                        var pixel = outputPointer + y * outputData.Stride + x * 3;
                        pixel[2] = ToByte((nx * 0.5f + 0.5f) * 255f);
                        pixel[1] = ToByte((ny * 0.5f + 0.5f) * 255f);
                        pixel[0] = ToByte((nz * 0.5f + 0.5f) * 255f);
                    }
                }
            }
        }
        finally
        {
            albedo.UnlockBits(sourceData);
            output.UnlockBits(outputData);
        }

        return output;
    }

    private static Bitmap CreateRoughnessMap(Bitmap albedo)
    {
        var output = new Bitmap(albedo.Width, albedo.Height, PixelFormat.Format24bppRgb);
        var rectangle = new Rectangle(0, 0, albedo.Width, albedo.Height);
        var sourceData = albedo.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var outputData = output.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        try
        {
            unsafe
            {
                var sourcePointer = (byte*)sourceData.Scan0;
                var outputPointer = (byte*)outputData.Scan0;

                for (var y = 0; y < albedo.Height; y++)
                {
                    for (var x = 0; x < albedo.Width; x++)
                    {
                        var sourcePixel = sourcePointer + y * sourceData.Stride + x * 3;
                        var value = Math.Max(176f, Math.Min(232f, 208f + (128f - Luminance(sourcePixel)) * 0.16f));
                        var roughness = ToByte(value);
                        var outputPixel = outputPointer + y * outputData.Stride + x * 3;
                        outputPixel[0] = roughness;
                        outputPixel[1] = roughness;
                        outputPixel[2] = roughness;
                    }
                }
            }
        }
        finally
        {
            albedo.UnlockBits(sourceData);
            output.UnlockBits(outputData);
        }

        return output;
    }

    private static unsafe float Luminance(byte* pixel)
    {
        return pixel[2] * 0.2126f + pixel[1] * 0.7152f + pixel[0] * 0.0722f;
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Max(0f, Math.Min(255f, value));
    }
}
'@

$compilerParameters = New-Object System.CodeDom.Compiler.CompilerParameters
$compilerParameters.GenerateInMemory = $true
$compilerParameters.CompilerOptions = '/unsafe'
$compilerParameters.ReferencedAssemblies.Add('System.Drawing.dll') | Out-Null
Add-Type -TypeDefinition $source -CompilerParameters $compilerParameters

$resolvedInput = (Resolve-Path -LiteralPath $InputPath).Path
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[TimberbornHdTexturePrep]::Run($resolvedInput, $resolvedOutput, $Size)

Write-Host "Prepared $Size x $Size soil textures in $resolvedOutput"
