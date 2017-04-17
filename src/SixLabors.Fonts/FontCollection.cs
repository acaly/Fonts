﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SixLabors.Fonts
{
    /// <summary>
    /// Provides a collection of fonts.
    /// </summary>
    public sealed class FontCollection : IFontCollection
    {
#if FILESYSTEM
        private static Lazy<SystemFontCollection> lazySystemFonts = new Lazy<SystemFontCollection>(() => new SystemFontCollection());

        /// <summary>
        /// Gets the globably installed system fonts.
        /// </summary>
        /// <value>
        /// The system fonts.
        /// </value>
        public static SystemFontCollection SystemFonts => lazySystemFonts.Value;
#endif

        Dictionary<string, List<IFontInstance>> instances = new Dictionary<string, List<IFontInstance>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, FontFamily> families = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="FontCollection"/> class.
        /// </summary>
        public FontCollection()
        {
        }

        /// <summary>
        /// Gets the collection of <see cref="FontFamily"/> objects associated with this <see cref="FontCollection"/>.
        /// </summary>
        /// <value>
        /// The families.
        /// </value>
        public IEnumerable<FontFamily> Families => this.families.Values.ToImmutableArray();

#if FILESYSTEM
        /// <summary>
        /// Installs a font from the specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>the description of the font just loaded.</returns>
        public Font Install(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                return this.Install(fs);
            }
        }
#endif

        /// <summary>
        /// Installs the specified font stream.
        /// </summary>
        /// <param name="fontStream">The font stream.</param>
        /// <returns>the description of the font just loaded.</returns>
        public Font Install(Stream fontStream)
        {
            FontInstance instance = FontInstance.LoadFont(fontStream);

            return Install(instance);
        }

        /// <summary>
        /// Finds the specified font family.
        /// </summary>
        /// <param name="fontFamily">The font family.</param>
        /// <returns>The family if installed otherwise null</returns>
        public FontFamily Find(string fontFamily)
        {
            if (this.families.ContainsKey(fontFamily))
            {
                return this.families[fontFamily];
            }

            return null;
        }

        internal IEnumerable<FontStyle> AvailibleStyles(string fontFamily)
        {
            return FindAll(fontFamily).Select(X => X.Description.Style).ToImmutableArray();
        }

        internal Font Install(IFontInstance instance)
        {
            if (instance != null && instance.Description != null)
            {
                lock (this.instances)
                {
                    if (!this.instances.ContainsKey(instance.Description.FontFamily))
                    {
                        this.instances.Add(instance.Description.FontFamily, new List<IFontInstance>(4));
                    }

                    if (!this.families.ContainsKey(instance.Description.FontFamily))
                    {
                        this.families.Add(instance.Description.FontFamily, new FontFamily(instance.Description.FontFamily, this));
                    }

                    this.instances[instance.Description.FontFamily].Add(instance);
                }

                return new Font(this.families[instance.Description.FontFamily], 12, instance.Description.Style);
            }

            return null;
        }

        internal IFontInstance Find(string fontFamily, FontStyle style)
        {
            if (!this.instances.ContainsKey(fontFamily))
            {
                return null;
            }

            // once we have to support verient fonts then we 
            List<IFontInstance> inFamily = this.instances[fontFamily];

            return inFamily.FirstOrDefault(x => x.Description.Style == style);
        }

        internal IEnumerable<IFontInstance> FindAll(string name)
        {
            if (!this.instances.ContainsKey(name))
            {
                return Enumerable.Empty<IFontInstance>();
            }

            // once we have to support verient fonts then we 
            return this.instances[name];
        }
    }
}
