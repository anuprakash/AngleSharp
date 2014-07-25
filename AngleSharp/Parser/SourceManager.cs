﻿namespace AngleSharp.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;

    /// <summary>
    /// Represents the source code manager.
    /// </summary>
    //[DebuggerStepThrough]
    sealed class SourceManager : IDisposable
    {
        #region Fields

        readonly Stack<Int32> _collengths;
        readonly StringBuilder _buffer;
        readonly TextStream _reader;

        Int32 _column;
        Int32 _row;
        Int32 _insertion;
        Char _current;
        Boolean _ended;

        #endregion

        #region ctor

        /// <summary>
        /// Prepares everything.
        /// </summary>
        SourceManager()
        {
            _buffer = new StringBuilder();
            _collengths = new Stack<Int32>();
            _column = 1;
            _row = 1;
        }

        /// <summary>
        /// Constructs a new instance of the source code manager.
        /// </summary>
        /// <param name="reader">The underlying text stream to read.</param>
        public SourceManager(TextStream reader)
            : this()
        {
            _reader = reader;
            ReadCurrent();
        }

        /// <summary>
        /// Constructs a new instance of the source code manager.
        /// </summary>
        /// <param name="source">The source code to manage.</param>
        internal SourceManager(String source)
            : this(new TextStream(source))
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets if the position is at the moment at the beginning.
        /// </summary>
        public Boolean IsBeginning 
        { 
            get { return _insertion < 2; } 
        }

        /// <summary>
        /// Gets or sets the insertion point.
        /// </summary>
        public Int32 InsertionPoint
        {
            get { return _insertion; }
            set
            {
                var delta = _insertion - value;

                while (delta > 0)
                {
                    BackUnsafe();
                    delta--;
                }

                while (delta < 0)
                {
                    AdvanceUnsafe();
                    delta++;
                }
            }
        }

        /// <summary>
        /// Gets the current line within the source code.
        /// </summary>
        public Int32 Line
        {
            get { return _row; }
        }

        /// <summary>
        /// Gets the current column within the source code.
        /// </summary>
        public Int32 Column
        {
            get { return _column; }
        }

        /// <summary>
        /// Gets the status of reading the source code, are we beyond the stream?
        /// </summary>
        public Boolean IsEnded
        {
            get { return _ended; }
        }

        /// <summary>
        /// Gets the status of reading the source code, is the EOF currently given?
        /// </summary>
        public Boolean IsEnding
        {
            get { return _current == Specification.EndOfFile; }
        }

        /// <summary>
        /// Gets the current character.
        /// </summary>
        public Char Current
        {
            get { return _current; }
        }

        /// <summary>
        /// Gets the next character (by advancing and returning the current character).
        /// </summary>
        [DebuggerHidden]
        public Char Next
        {
            get { Advance(); return _current; }
        }

        /// <summary>
        /// Gets the previous character (by rewinding and returning the current character).
        /// </summary>
        [DebuggerHidden]
        public Char Previous
        {
            get { Back(); return _current; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Resets the insertion point to the end of the buffer.
        /// </summary>
        [DebuggerStepThrough]
        public void ResetInsertionPoint()
        {
            InsertionPoint = _buffer.Length;
        }

        /// <summary>
        /// Advances one character in the source code.
        /// </summary>
        /// <returns>The current source manager.</returns>
        [DebuggerStepThrough]
        public void Advance()
        {
            if (!IsEnding)
                AdvanceUnsafe();
            else if (!_ended)
                _ended = true;
        }

        /// <summary>
        /// Advances n characters in the source code.
        /// </summary>
        /// <param name="n">The number of characters to advance.</param>
        [DebuggerStepThrough]
        public void Advance(Int32 n)
        {
            while (n-- > 0 && !IsEnding)
                AdvanceUnsafe();
        }

        /// <summary>
        /// Moves back one character in the source code.
        /// </summary>
        [DebuggerStepThrough]
        public void Back()
        {
            _ended = false;

            if (!IsBeginning)
                BackUnsafe();
        }

        /// <summary>
        /// Moves back n characters in the source code.
        /// </summary>
        /// <param name="n">The number of characters to rewind.</param>
        [DebuggerStepThrough]
        public void Back(Int32 n)
        {
            _ended = false;

            while (n-- > 0 && !IsBeginning)
                BackUnsafe();
        }

        /// <summary>
        /// Looks if the current character / next characters match a certain string.
        /// </summary>
        /// <param name="s">The string to compare to.</param>
        /// <param name="ignoreCase">Optional flag to unignore the case sensitivity.</param>
        /// <returns>The status of the check.</returns>
        [DebuggerStepThrough]
        public Boolean ContinuesWith(String s, Boolean ignoreCase = true)
        {
            for (var index = 0; index < s.Length; index++)
            {
                var chr = _current;

                if (ignoreCase)
                {
                    if (chr.IsUppercaseAscii() && s[index].IsLowercaseAscii())
                        chr = Char.ToLower(chr);
                    else if (chr.IsLowercaseAscii() && s[index].IsUppercaseAscii())
                        chr = Char.ToUpper(chr);
                }

                if (s[index] != chr)
                {
                    Back(index);
                    return false;
                }

                Advance();
            }

            Back(s.Length);
            return true;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Reads the current character (from the stream or
        /// from the buffer).
        /// </summary>
        [DebuggerStepThrough]
        void ReadCurrent()
        {
            if (_insertion < _buffer.Length)
            {
                _current = _buffer[_insertion];
            }
            else
            {
                _current = _reader.Read();
                _buffer.Append(_current);
            }

            _insertion++;
        }

        /// <summary>
        /// Just advances one character without checking
        /// if the end is already reached.
        /// </summary>
        [DebuggerStepThrough]
        void AdvanceUnsafe()
        {
            if (_current.IsLineBreak())
            {
                _collengths.Push(_column);
                _column = 1;
                _row++;
            }
            else
                _column++;

            ReadCurrent();
        }

        /// <summary>
        /// Just goes back one character without checking
        /// if the beginning is already reached.
        /// </summary>
        [DebuggerStepThrough]
        void BackUnsafe()
        {
            _insertion--;

            if (_insertion == 0)
            {
                _column = 0;
                _current = Specification.Null;
                return;
            }

            _current = _buffer[_insertion - 1];

            if (_current.IsLineBreak())
            {
                _column = _collengths.Count != 0 ? _collengths.Pop() : 1;
                _row--;
            }
            else
                _column--;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes all disposable objects.
        /// </summary>
        public void Dispose()
        {
            _reader.Dispose();
        }

        #endregion
    }
}
