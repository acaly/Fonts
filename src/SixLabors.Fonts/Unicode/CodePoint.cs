// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Text;

namespace SixLabors.Fonts.Unicode
{
    /// <summary>
    /// Represents a Unicode value ([ U+0000..U+10FFFF ], inclusive).
    /// </summary>
    internal readonly struct CodePoint
    {
        private const byte IsWhiteSpaceFlag = 0x80;
        private const byte UnicodeCategoryMask = 0x1F;

        private readonly uint value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodePoint"/> struct.
        /// </summary>
        /// <param name="value">The value to create the codepoint.</param>
        public CodePoint(int value)
            : this((uint)value)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CodePoint"/> struct.
        /// </summary>
        /// <param name="value">The value to create the codepoint.</param>
        public CodePoint(uint value)
        {
            Guard.IsTrue(UnicodeUtility.IsValidCodePoint(value), nameof(value), "Must be in [ U+0000..U+10FFFF ], inclusive.");

            this.value = value;
            this.Value = (int)value;
        }

        // Contains information about the ASCII character range [ U+0000..U+007F ], with:
        // - 0x80 bit if set means 'is whitespace'
        // - 0x40 bit if set means 'is letter or digit'
        // - 0x20 bit is reserved for future use
        // - bottom 5 bits are the UnicodeCategory of the character
        private static ReadOnlySpan<byte> AsciiCharInfo => new byte[]
        {
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x8E, 0x8E, 0x8E, 0x8E, 0x8E, 0x0E, 0x0E, // U+0000..U+000F
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, // U+0010..U+001F
            0x8B, 0x18, 0x18, 0x18, 0x1A, 0x18, 0x18, 0x18, 0x14, 0x15, 0x18, 0x19, 0x18, 0x13, 0x18, 0x18, // U+0020..U+002F
            0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x18, 0x18, 0x19, 0x19, 0x19, 0x18, // U+0030..U+003F
            0x18, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, // U+0040..U+004F
            0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x14, 0x18, 0x15, 0x1B, 0x12, // U+0050..U+005F
            0x1B, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, // U+0060..U+006F
            0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x14, 0x19, 0x15, 0x19, 0x0E, // U+0070..U+007F
        };

        /// <summary>
        /// Gets the Unicode replacement character U+FFFD.
        /// </summary>
        public static CodePoint ReplacementCodePoint { get; } = new CodePoint(0xFFFD);

        /// <summary>
        /// Gets a value indicating whether this value is within the BMP ([ U+0000..U+FFFF ])
        /// and therefore representable by a single UTF-16 code unit.
        /// </summary>
        public bool IsBmp => UnicodeUtility.IsBmpCodePoint(this.value);

        /// <summary>
        /// Gets a value indicating whether this value is ASCII ([ U+0000..U+007F ])
        /// and therefore representable by a single UTF-8 code unit.
        /// </summary>
        public bool IsAscii => UnicodeUtility.IsAsciiCodePoint(this.value);

        /// <summary>
        /// Gets a value indicating whether this <see cref="CodePoint"/> is a break char.
        /// </summary>
        public bool IsBreakChar
        {
            // Copied from Avalonia.
            // TODO: How do we confirm this?
            get
            {
                switch (this.value)
                {
                    case 0x000A: // LINE FEED (LF)
                    case 0x000B: // LINE TABULATION
                    case 0x000C: // FORM FEED (FF)
                    case 0x000D: // CARRIAGE RETURN (CR)
                    case 0x0085: // NEXT LINE (NEL)
                    case 0x2028: // LINE SEPARATOR
                    case 0x2029: // PARAGRAPH SEPARATOR
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets the Unicode value as an integer.
        /// </summary>
        public readonly int Value { get; }

        /// <summary>
        /// Gets a value indicating whether the given codepoint is white space.
        /// </summary>
        public static bool IsWhiteSpace(CodePoint codePoint)
        {
            if (codePoint.IsAscii)
            {
                return (AsciiCharInfo[codePoint.Value] & IsWhiteSpaceFlag) != 0;
            }

            // Only BMP code points can be white space, so only call into CharUnicodeInfo
            // if the incoming value is within the BMP.
            return codePoint.IsBmp && GetBidiType(codePoint).CharacterType == BidiCharacterType.WS;
        }

        /// <summary>
        /// Returns the number of codepoints in a given string.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The <see cref="int"/>.</returns>
        public static int GetCodePointCount(string text) => Encoding.UTF32.GetByteCount(text) / sizeof(uint);

        /// <summary>
        /// Gets the <see cref="BidiType"/> for the given codepoint.
        /// </summary>
        public static BidiType GetBidiType(CodePoint codePoint)
            => new BidiType(codePoint);

        /// <summary>
        /// Gets the <see cref="LineBreakClass"/> for the given codepoint.
        /// </summary>
        public static LineBreakClass GetLineBreakClass(CodePoint codePoint)
            => UnicodeData.GetLineBreakClass(codePoint.Value);

        /// <summary>
        /// Gets the <see cref="GraphemeClusterClass"/> for the given codepoint.
        /// </summary>
        public static GraphemeClusterClass GraphemeClusterClass(CodePoint codePoint)
            => UnicodeData.GetGraphemeClusterClass(codePoint.Value);

        /// <summary>
        /// Gets the <see cref="UnicodeCategory"/> for the given codepoint.
        /// </summary>
        public static UnicodeCategory GetGeneralCategory(CodePoint codePoint)
        {
            if (codePoint.IsAscii)
            {
                return (UnicodeCategory)(AsciiCharInfo[codePoint.Value] & UnicodeCategoryMask);
            }

            return UnicodeData.GetUnicodeCategory(codePoint.Value);
        }

        /// <summary>
        /// Reads the <see cref="CodePoint"/> at specified position.
        /// </summary>
        /// <param name="text">The buffer to read from.</param>
        /// <param name="index">The index to read at.</param>
        /// <returns>The <see cref="CodePoint"/>.</returns>
        public static CodePoint ReadAt(string text, int index) => ReadAt(text, index, out int _);

        /// <summary>
        /// Reads the <see cref="CodePoint"/> at specified position.
        /// </summary>
        /// <param name="text">The buffer to read from.</param>
        /// <param name="index">The index to read at.</param>
        /// <param name="count">The count of character that were read.</param>
        /// <returns>The <see cref="CodePoint"/>.</returns>
        public static CodePoint ReadAt(string text, int index, out int count)
        {
            count = 1;

            if (index > text.Length)
            {
                return ReplacementCodePoint;
            }

            // Optimistically assume input is within BMP.
            uint code = text[index];

            if (UnicodeUtility.IsSurrogateCodePoint(code))
            {
                uint hi, low;

                // High surrogate
                if (UnicodeUtility.IsHighSurrogateCodePoint(code))
                {
                    hi = code;

                    if (index + 1 == text.Length)
                    {
                        return ReplacementCodePoint;
                    }

                    low = text[index + 1];

                    if (UnicodeUtility.IsLowSurrogateCodePoint(low))
                    {
                        count = 2;
                        return new CodePoint(UnicodeUtility.GetScalarFromUtf16SurrogatePair(hi, low));
                    }

                    return ReplacementCodePoint;
                }

                // Low surrogate
                if (UnicodeUtility.IsLowSurrogateCodePoint(code))
                {
                    if (index == 0)
                    {
                        return ReplacementCodePoint;
                    }

                    hi = text[index - 1];
                    low = code;

                    if (UnicodeUtility.IsHighSurrogateCodePoint(hi))
                    {
                        count = 2;
                        return new CodePoint(UnicodeUtility.GetScalarFromUtf16SurrogatePair(hi, low));
                    }

                    return ReplacementCodePoint;
                }
            }

            return new CodePoint(code);
        }
    }
}
