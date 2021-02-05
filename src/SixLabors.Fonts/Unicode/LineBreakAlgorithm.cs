// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace SixLabors.Fonts.Unicode
{
    /// <summary>
    /// Implementation of the Unicode Line Break Algorithm. UAX:14
    /// <see href="https://www.unicode.org/reports/tr14/tr14-37.html"/>
    /// </summary>
    internal ref struct LineBreakAlgorithm
    {
        private readonly ReadOnlySpan<char> source;
        private int charPosition;
        private readonly int pointsLength;
        private int position;
        private int lastPosition;
        private LineBreakClass currentClass;
        private LineBreakClass nextClass;
        private bool first;
        private int alphaNumericCount;
        private bool lb8a;
        private bool lb21a;
        private bool lb22ex;
        private bool lb24ex;
        private bool lb25ex;
        private bool lb30;
        private int lb30a;
        private bool lb31;

        public LineBreakAlgorithm(ReadOnlySpan<char> source)
            : this()
        {
            this.source = source;
            this.pointsLength = CodePoint.GetCodePointCount(source);
            this.charPosition = 0;
            this.position = 0;
            this.lastPosition = 0;
            this.currentClass = LineBreakClass.XX;
            this.nextClass = LineBreakClass.XX;
            this.first = true;
            this.lb8a = false;
            this.lb21a = false;
            this.lb22ex = false;
            this.lb24ex = false;
            this.lb25ex = false;
            this.alphaNumericCount = 0;
            this.lb31 = false;
            this.lb30 = false;
            this.lb30a = 0;
        }

        /// <summary>
        /// Returns the line break from the current source if one is found.
        /// </summary>
        /// <param name="lineBreak">
        /// When this method returns, contains the value associate with the break;
        /// otherwise, the default value.
        /// This parameter is passed uninitialized.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public bool TryGetNextBreak(out LineBreak lineBreak)
        {
            // Get the first char if we're at the beginning of the string.
            if (this.first)
            {
                LineBreakClass firstClass = this.NextCharClass();
                this.first = false;
                this.currentClass = this.MapFirst(firstClass);
                this.nextClass = firstClass;
                this.lb8a = firstClass == LineBreakClass.ZWJ;
                this.lb30a = 0;
            }

            while (this.position < this.pointsLength)
            {
                this.lastPosition = this.position;
                LineBreakClass lastClass = this.nextClass;
                this.nextClass = this.NextCharClass();

                // Explicit newline
                switch (this.currentClass)
                {
                    case LineBreakClass.BK:
                    case LineBreakClass.CR when this.nextClass != LineBreakClass.LF:
                        this.currentClass = this.MapFirst(this.nextClass);
                        lineBreak = new LineBreak(this.FindPriorNonWhitespace(this.lastPosition), this.lastPosition, true);
                        return true;
                }

                bool? shouldBreak = this.GetSimpleBreak() ?? (bool?)this.GetPairTableBreak(lastClass);

                // Rule LB8a
                this.lb8a = this.nextClass == LineBreakClass.ZWJ;

                if (shouldBreak.Value)
                {
                    lineBreak = new LineBreak(this.FindPriorNonWhitespace(this.lastPosition), this.lastPosition, false);
                    return true;
                }
            }

            if (this.position >= this.pointsLength && this.lastPosition < this.pointsLength)
            {
                this.lastPosition = this.pointsLength;
                bool required = false;
                switch (this.currentClass)
                {
                    case LineBreakClass.BK:
                    case LineBreakClass.CR when this.nextClass != LineBreakClass.LF:
                        required = true;
                        break;
                }

                lineBreak = new LineBreak(this.FindPriorNonWhitespace(this.pointsLength), this.lastPosition, required);
                return true;
            }

            lineBreak = default;
            return false;
        }

        private LineBreakClass MapClass(CodePoint cp, LineBreakClass c)
        {
            // LB 1
            // ==========================================
            // Resolved Original    General_Category
            // ==========================================
            // AL       AI, SG, XX  Any
            // CM       SA          Only Mn or Mc
            // AL       SA          Any except Mn and Mc
            // NS       CJ          Any
            switch (c)
            {
                case LineBreakClass.AI:
                case LineBreakClass.SG:
                case LineBreakClass.XX:
                    return LineBreakClass.AL;

                case LineBreakClass.SA:
                    UnicodeCategory category = CodePoint.GetGeneralCategory(cp);
                    return (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark)
                        ? LineBreakClass.CM
                        : LineBreakClass.AL;

                case LineBreakClass.CJ:
                    return LineBreakClass.NS;

                default:
                    return c;
            }
        }

        private LineBreakClass MapFirst(LineBreakClass c)
        {
            switch (c)
            {
                case LineBreakClass.LF:
                case LineBreakClass.NL:
                    return LineBreakClass.BK;

                case LineBreakClass.SP:
                    return LineBreakClass.WJ;

                default:
                    return c;
            }
        }

        private bool IsAlphaNumeric(LineBreakClass cls)
            => cls == LineBreakClass.AL
            || cls == LineBreakClass.HL
            || cls == LineBreakClass.NU;

        private LineBreakClass PeekNextCharClass()
        {
            var cp = CodePoint.DecodeFromUtf16At(this.source, this.charPosition);
            return this.MapClass(cp, CodePoint.GetLineBreakClass(cp));
        }

        // Get the next character class
        private LineBreakClass NextCharClass()
        {
            var cp = CodePoint.DecodeFromUtf16At(this.source, this.charPosition, out int count);
            LineBreakClass cls = this.MapClass(cp, CodePoint.GetLineBreakClass(cp));
            this.charPosition += count;
            this.position++;

            // Keep track of alphanumeric + any combining marks.
            // This is used for LB22 and LB30.
            if (this.IsAlphaNumeric(this.currentClass) || (this.alphaNumericCount > 0 && cls == LineBreakClass.CM))
            {
                this.alphaNumericCount++;
            }

            // Track combining mark exceptions. LB22
            if (cls == LineBreakClass.CM)
            {
                switch (this.currentClass)
                {
                    case LineBreakClass.BK:
                    case LineBreakClass.CB:
                    case LineBreakClass.EX:
                    case LineBreakClass.LF:
                    case LineBreakClass.NL:
                    case LineBreakClass.SP:
                    case LineBreakClass.ZW:
                    case LineBreakClass.CR:
                        this.lb22ex = true;
                        break;
                }
            }

            // Track combining mark exceptions. LB31
            if (this.first && cls == LineBreakClass.CM)
            {
                this.lb31 = true;
            }

            if (cls == LineBreakClass.CM)
            {
                switch (this.currentClass)
                {
                    case LineBreakClass.BK:
                    case LineBreakClass.CB:
                    case LineBreakClass.EX:
                    case LineBreakClass.LF:
                    case LineBreakClass.NL:
                    case LineBreakClass.SP:
                    case LineBreakClass.ZW:
                    case LineBreakClass.CR:
                    case LineBreakClass.ZWJ:
                        this.lb31 = true;
                        break;
                }
            }

            if (this.first
                && (cls == LineBreakClass.PO || cls == LineBreakClass.PR || cls == LineBreakClass.SP))
            {
                this.lb31 = true;
            }

            if (this.currentClass == LineBreakClass.AL
                && (cls == LineBreakClass.PO || cls == LineBreakClass.PR || cls == LineBreakClass.SP))
            {
                this.lb31 = true;
            }

            // Reset LB31 if next is U+0028 (Left Opening Parenthesis)
            if (this.lb31
                && this.currentClass != LineBreakClass.PO
                && this.currentClass != LineBreakClass.PR
                && cls == LineBreakClass.OP && cp.Value == 0x0028)
            {
                this.lb31 = false;
            }

            // Rule LB24
            if (this.first && (cls == LineBreakClass.CL || cls == LineBreakClass.CP))
            {
                this.lb24ex = true;
            }

            // Rule LB25
            if (this.first
                && (cls == LineBreakClass.CL || cls == LineBreakClass.IS || cls == LineBreakClass.SY))
            {
                this.lb25ex = true;
            }

            if (cls == LineBreakClass.SP || cls == LineBreakClass.WJ || cls == LineBreakClass.AL)
            {
                LineBreakClass next = this.PeekNextCharClass();
                if (next == LineBreakClass.CL || next == LineBreakClass.IS || next == LineBreakClass.SY)
                {
                    this.lb25ex = true;
                }
            }

            // AlphaNumeric + and combining marks can break for OP except.
            // - U+0028 (Left Opening Parenthesis)
            // - U+005B (Opening Square Bracket)
            // - U+007B (Left Curly Bracket)
            // See custom colums|rules in the text pair table.
            // https://www.unicode.org/Public/13.0.0/ucd/auxiliary/LineBreakTest.html
            this.lb30 = this.alphaNumericCount > 0
                && cls == LineBreakClass.OP
                && cp.Value != 0x0028
                && cp.Value != 0x005B
                && cp.Value != 0x007B;

            return cls;
        }

        private bool? GetSimpleBreak()
        {
            // handle classes not handled by the pair table
            switch (this.nextClass)
            {
                case LineBreakClass.SP:
                    return false;

                case LineBreakClass.BK:
                case LineBreakClass.LF:
                case LineBreakClass.NL:
                    this.currentClass = LineBreakClass.BK;
                    return false;

                case LineBreakClass.CR:
                    this.currentClass = LineBreakClass.CR;
                    return false;
            }

            return null;
        }

        private bool GetPairTableBreak(LineBreakClass lastClass)
        {
            // If not handled already, use the pair table
            bool shouldBreak = false;
            switch (LineBreakPairTable.Table[(int)this.currentClass][(int)this.nextClass])
            {
                case LineBreakPairTable.DIBRK: // Direct break
                    shouldBreak = true;
                    break;

                // TODO: Rewrite this so that it defaults to true and rules are set as exceptions.
                case LineBreakPairTable.INBRK: // Possible indirect break

                    // LB31
                    if (this.lb31 && this.nextClass == LineBreakClass.OP)
                    {
                        shouldBreak = true;
                        this.lb31 = false;
                        break;
                    }

                    // LB30
                    if (this.lb30)
                    {
                        shouldBreak = true;
                        this.lb30 = false;
                        this.alphaNumericCount = 0;
                        break;
                    }

                    // LB25
                    if (this.lb25ex && (this.nextClass == LineBreakClass.PR || this.nextClass == LineBreakClass.NU))
                    {
                        shouldBreak = true;
                        this.lb25ex = false;
                        break;
                    }

                    // LB24
                    if (this.lb24ex && (this.nextClass == LineBreakClass.PO || this.nextClass == LineBreakClass.PR))
                    {
                        shouldBreak = true;
                        this.lb24ex = false;
                        break;
                    }

                    // LB18
                    shouldBreak = lastClass == LineBreakClass.SP;
                    break;

                case LineBreakPairTable.CIBRK:
                    shouldBreak = lastClass == LineBreakClass.SP;
                    if (!shouldBreak)
                    {
                        return false;
                    }

                    break;

                case LineBreakPairTable.CPBRK: // prohibited for combining marks
                    if (lastClass != LineBreakClass.SP)
                    {
                        return false;
                    }

                    break;

                case LineBreakPairTable.PRBRK:
                    break;
            }

            // Rule LB22
            if (this.nextClass == LineBreakClass.IN)
            {
                switch (lastClass)
                {
                    case LineBreakClass.BK:
                    case LineBreakClass.CB:
                    case LineBreakClass.EX:
                    case LineBreakClass.LF:
                    case LineBreakClass.NL:
                    case LineBreakClass.SP:
                    case LineBreakClass.ZW:

                        // Allow break
                        break;
                    case LineBreakClass.CM:
                        if (this.lb22ex)
                        {
                            // Allow break
                            this.lb22ex = false;
                            break;
                        }

                        shouldBreak = false;
                        break;
                    default:
                        shouldBreak = false;
                        break;
                }
            }

            if (this.lb8a)
            {
                shouldBreak = false;
            }

            // Rule LB21a
            if (this.lb21a && (this.currentClass == LineBreakClass.HY || this.currentClass == LineBreakClass.BA))
            {
                shouldBreak = false;
                this.lb21a = false;
            }
            else
            {
                this.lb21a = this.currentClass == LineBreakClass.HL;
            }

            // Rule LB30a
            if (this.currentClass == LineBreakClass.RI)
            {
                this.lb30a++;
                if (this.lb30a == 2 && (this.nextClass == LineBreakClass.RI))
                {
                    shouldBreak = true;
                    this.lb30a = 0;
                }
            }
            else
            {
                this.lb30a = 0;
            }

            this.currentClass = this.nextClass;

            return shouldBreak;
        }

        private int FindPriorNonWhitespace(int from)
        {
            if (from > 0)
            {
                var cp = CodePoint.DecodeFromUtf16At(this.source, from - 1, out int count);
                LineBreakClass cls = CodePoint.GetLineBreakClass(cp);

                if (cls == LineBreakClass.BK || cls == LineBreakClass.LF || cls == LineBreakClass.CR)
                {
                    from -= count;
                }
            }

            while (from > 0)
            {
                var cp = CodePoint.DecodeFromUtf16At(this.source, from - 1, out int count);
                LineBreakClass cls = CodePoint.GetLineBreakClass(cp);

                if (cls == LineBreakClass.SP)
                {
                    from -= count;
                }
                else
                {
                    break;
                }
            }

            return from;
        }
    }
}