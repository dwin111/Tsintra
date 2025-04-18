using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tsintra.MarketplaceAgent.Models.Core // Changed namespace
{
    public class ImageSettings
    {
        public int Width { get; set; } = 1024; // Default width
        public int Height { get; set; } = 1024; // Default height
        public string? Format { get; set; } = "png"; // Default format
        public string? SourceDirectory { get; set; } // Added SourceDirectory property
        // Add other relevant image settings here
    }
} 