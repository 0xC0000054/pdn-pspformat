﻿////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-pspformat, a FileType plugin for Paint.NET
//
// Copyright (c) 2011-2015, 2019, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;

namespace PaintShopProFiletype.IO
{
    // Adapted from 'Problem and Solution: The Terrible Inefficiency of FileStream and BinaryReader'
    // https://jacksondunstan.com/articles/3568

    internal sealed class EndianBinaryReader : PaintDotNet.Disposable
    {
        private const int MaxBufferSize = 4096;

        private static ReadOnlySpan<byte> AsciiWhiteSpaceChars => new byte[]
        {
            0x09, // horizontal tab
            0x10, // line feed
            0x11, // vertical tab
            0x12, // form feed
            0x13, // carriage return
            0x20, // space
        };

#pragma warning disable IDE0032 // Use auto property
        private int readOffset;
        private int readLength;

        private readonly Stream stream;
        private readonly byte[] buffer;
        private readonly int bufferSize;
        private readonly Endianess endianess;
        private readonly bool leaveOpen;
#pragma warning restore IDE0032 // Use auto property

        /// <summary>
        /// Initializes a new instance of the <see cref="EndianBinaryReader"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="byteOrder">The byte order of the stream.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public EndianBinaryReader(Stream stream, Endianess byteOrder) : this(stream, byteOrder, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EndianBinaryReader"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="byteOrder">The byte order of the stream.</param>
        /// <param name="leaveOpen">
        /// <see langword="true"/> to leave the stream open after the EndianBinaryReader is disposed; otherwise, <see langword="false"/>
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public EndianBinaryReader(Stream stream, Endianess byteOrder, bool leaveOpen)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.bufferSize = (int)Math.Min(stream.Length, MaxBufferSize);
            this.buffer = new byte[this.bufferSize];
            this.endianess = byteOrder;
            this.leaveOpen = leaveOpen;

            this.readOffset = 0;
            this.readLength = 0;
        }

        public Endianess Endianess => this.endianess;

        /// <summary>
        /// Gets the length of the stream.
        /// </summary>
        /// <value>
        /// The length of the stream.
        /// </value>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long Length
        {
            get
            {
                VerifyNotDisposed();

                return this.stream.Length;
            }
        }

        /// <summary>
        /// Gets or sets the position in the stream.
        /// </summary>
        /// <value>
        /// The position in the stream.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">value is negative.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long Position
        {
            get
            {
                VerifyNotDisposed();

                return this.stream.Position - this.readLength + this.readOffset;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                VerifyNotDisposed();

                long current = this.Position;

                if (value != current)
                {
                    long bufferStartOffset = current - this.readOffset;
                    long bufferEndOffset = bufferStartOffset + this.readLength;

                    // Avoid reading from the stream if the offset is within the current buffer.
                    if (value >= bufferStartOffset && value <= bufferEndOffset)
                    {
                        this.readOffset = (int)(value - bufferStartOffset);
                    }
                    else
                    {
                        // Invalidate the existing buffer.
                        this.readOffset = 0;
                        this.readLength = 0;
                        this.stream.Seek(value, SeekOrigin.Begin);
                    }
                }
            }
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream, starting from a specified point in the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The starting offset in the array.</param>
        /// <param name="count">The count.</param>
        /// <returns>The number of bytes read from the stream.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int Read(byte[] bytes, int offset, int count)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            VerifyNotDisposed();

            if (count == 0)
            {
                return 0;
            }

            if ((this.readOffset + count) <= this.readLength)
            {
                Buffer.BlockCopy(this.buffer, this.readOffset, bytes, offset, count);
                this.readOffset += count;

                return count;
            }
            else
            {
                // Ensure that any bytes at the end of the current buffer are included.
                int bytesUnread = this.readLength - this.readOffset;

                if (bytesUnread > 0)
                {
                    Buffer.BlockCopy(this.buffer, this.readOffset, bytes, offset, bytesUnread);
                }

                // Invalidate the existing buffer.
                this.readOffset = 0;
                this.readLength = 0;

                int totalBytesRead = bytesUnread;

                totalBytesRead += this.stream.Read(bytes, offset + bytesUnread, count - bytesUnread);

                return totalBytesRead;
            }
        }

        /// <summary>
        /// Reads the next byte from the current stream.
        /// </summary>
        /// <returns>The next byte read from the current stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public byte ReadByte()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(byte));

            byte val = this.buffer[this.readOffset];
            this.readOffset += sizeof(byte);

            return val;
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="count">The number of bytes to read..</param>
        /// <returns>An array containing the specified bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            VerifyNotDisposed();

            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] bytes = new byte[count];

            if ((this.readOffset + count) <= this.readLength)
            {
                Buffer.BlockCopy(this.buffer, this.readOffset, bytes, 0, count);
                this.readOffset += count;
            }
            else
            {
                // Ensure that any bytes at the end of the current buffer are included.
                int bytesUnread = this.readLength - this.readOffset;

                if (bytesUnread > 0)
                {
                    Buffer.BlockCopy(this.buffer, this.readOffset, bytes, 0, bytesUnread);
                }

                int numBytesToRead = count - bytesUnread;
                int numBytesRead = bytesUnread;
                do
                {
                    int n = this.stream.Read(bytes, numBytesRead, numBytesToRead);

                    if (n == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    numBytesRead += n;
                    numBytesToRead -= n;

                } while (numBytesToRead > 0);

                // Invalidate the existing buffer.
                this.readOffset = 0;
                this.readLength = 0;
            }

            return bytes;
        }

        /// <summary>
        /// Reads a 8-byte floating point value.
        /// </summary>
        /// <returns>The 8-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe double ReadDouble()
        {
            ulong temp = ReadUInt64();

            return *(double*)&temp;
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void ReadExactly(Span<byte> buffer)
        {
            VerifyNotDisposed();

            Span<byte> destination = buffer;

            while (destination.Length > 0)
            {
                int bytesRead = ReadInternal(destination);

                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }

                destination = destination.Slice(bytesRead);
            }
        }

        /// <summary>
        /// Reads a 2-byte signed integer.
        /// </summary>
        /// <returns>The 2-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        /// <summary>
        /// Reads a 2-byte unsigned integer.
        /// </summary>
        /// <returns>The 2-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ushort ReadUInt16()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(ushort));

            ushort value = Unsafe.ReadUnaligned<ushort>(ref this.buffer[this.readOffset]);

            switch (this.endianess)
            {
                case Endianess.Big:
                    if (BitConverter.IsLittleEndian)
                    {
                        value = BinaryPrimitives.ReverseEndianness(value);
                    }
                    break;
                case Endianess.Little:
                    if (!BitConverter.IsLittleEndian)
                    {
                        value = BinaryPrimitives.ReverseEndianness(value);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unsupported byte order: " + this.endianess.ToString());
            }

            this.readOffset += sizeof(ushort);

            return value;
        }

        /// <summary>
        /// Reads a 4-byte signed integer.
        /// </summary>
        /// <returns>The 4-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer.
        /// </summary>
        /// <returns>The 4-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public uint ReadUInt32()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(uint));

            uint value = Unsafe.ReadUnaligned<uint>(ref this.buffer[this.readOffset]);

            switch (this.endianess)
            {
                case Endianess.Big:
                    if (BitConverter.IsLittleEndian)
                    {
                        value = BinaryPrimitives.ReverseEndianness(value);
                    }
                    break;
                case Endianess.Little:
                    if (!BitConverter.IsLittleEndian)
                    {
                        value = BinaryPrimitives.ReverseEndianness(value);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unsupported byte order: " + this.endianess.ToString());
            }

            this.readOffset += sizeof(uint);

            return value;
        }

        /// <summary>
        /// Reads a 4-byte floating point value.
        /// </summary>
        /// <returns>The 4-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe float ReadSingle()
        {
            uint temp = ReadUInt32();

            return *(float*)&temp;
        }

        /// <summary>
        /// Reads a 8-byte signed integer.
        /// </summary>
        /// <returns>The 8-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long ReadInt64()
        {
            return (long)ReadUInt64();
        }

        /// <summary>
        /// Reads a 8-byte unsigned integer.
        /// </summary>
        /// <returns>The 8-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ulong ReadUInt64()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(ulong));

            ulong value = Unsafe.ReadUnaligned<ulong>(ref this.buffer[this.readOffset]);

            switch (this.endianess)
            {
                case Endianess.Big:
                    if (BitConverter.IsLittleEndian)
                    {
                        value = BinaryPrimitives.ReverseEndianness(value);
                    }
                    break;
                case Endianess.Little:
                    if (!BitConverter.IsLittleEndian)
                    {
                        value = BinaryPrimitives.ReverseEndianness(value);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unsupported byte order: " + this.endianess.ToString());
            }

            this.readOffset += sizeof(ulong);

            return value;
        }

        /// <summary>
        /// Reads an ASCII string from the stream.
        /// </summary>
        /// <param name="length">The length of the string.</param>
        /// <param name="options">The string read options.</param>
        /// <returns>The string.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public string ReadAsciiString(int length, StringReadOptions options)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            VerifyNotDisposed();

            if (length == 0)
            {
                return string.Empty;
            }

            ReadOnlySpan<byte> bytes;

            if (length <= this.bufferSize)
            {
                EnsureBuffer(length);
                bytes = new ReadOnlySpan<byte>(this.buffer, this.readOffset, length);

                this.readOffset += length;
            }
            else
            {
                bytes = ReadBytes(length);
            }

            if (options.HasFlag(StringReadOptions.TrimNullTerminator))
            {
                // Trim the string to the first null-terminator.
                // IndexOf should be faster than calling TrimEnd, as the strings this is used with
                // will normally be short with a lot of trailing 0 bytes as padding.
                int terminatorIndex = bytes.IndexOf((byte)0);

                if (terminatorIndex >= 0)
                {
                    bytes = bytes.Slice(0, terminatorIndex);
                }
            }

            if (options.HasFlag(StringReadOptions.TrimWhiteSpace))
            {
                bytes = bytes.Trim(AsciiWhiteSpaceChars);
            }

            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.leaveOpen)
            {
                this.stream.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Ensures that the buffer contains at least the number of bytes requested.
        /// </summary>
        /// <param name="count">The minimum number of bytes the buffer should contain.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        private void EnsureBuffer(int count)
        {
            if ((this.readOffset + count) > this.readLength)
            {
                FillBuffer(count);
            }
        }

        /// <summary>
        /// Fills the buffer with at least the number of bytes requested.
        /// </summary>
        /// <param name="minBytes">The minimum number of bytes to place in the buffer.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        private void FillBuffer(int minBytes)
        {
            if (!TryFillBuffer(minBytes))
            {
                ThrowEndOfStreamException();
            }

            static void ThrowEndOfStreamException()
            {
                throw new EndOfStreamException();
            }
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="destination">The span.</param>
        /// <returns>The number of bytes read from the stream.</returns>
        private int ReadInternal(Span<byte> destination)
        {
            int count = destination.Length;

            if (count == 0)
            {
                return 0;
            }

            if ((this.readOffset + count) <= this.readLength)
            {
                new ReadOnlySpan<byte>(this.buffer, this.readOffset, count).CopyTo(destination);
                this.readOffset += count;

                return count;
            }
            else
            {
                int totalBytesRead;

                if (count < this.bufferSize)
                {
                    // This is an optimization for sequentially reading small ranges of bytes
                    // from a file.
                    // For example, a file signature that will be followed by other header data.
                    //
                    // TryFillBuffer may return fewer bytes than were requested if the end of the
                    // stream has been reached.
                    totalBytesRead = TryFillBuffer(count) ? count : this.readLength;

                    if (totalBytesRead > 0)
                    {
                        new ReadOnlySpan<byte>(this.buffer, this.readOffset, totalBytesRead).CopyTo(destination.Slice(0, totalBytesRead));

                        this.readOffset += totalBytesRead;
                    }
                }
                else
                {
                    // Ensure that any bytes at the end of the current buffer are included.
                    int bytesUnread = this.readLength - this.readOffset;

                    if (bytesUnread > 0)
                    {
                        new ReadOnlySpan<byte>(this.buffer, this.readOffset, bytesUnread).CopyTo(destination);
                    }

                    totalBytesRead = bytesUnread;
                    int bytesRemaining = count - bytesUnread;

                    // Invalidate the existing buffer.
                    this.readOffset = 0;
                    this.readLength = 0;

                    totalBytesRead += this.stream.Read(destination.Slice(bytesUnread, bytesRemaining));
                }

                return totalBytesRead;
            }
        }

        /// <summary>
        /// Attempts to fill the buffer with at least the number of bytes requested.
        /// </summary>
        /// <param name="minBytes">The minimum number of bytes to place in the buffer.</param>
        /// <returns>
        /// <see langword="true"/> if the buffer contains at least <paramref name="minBytes"/>; otherwise, <see langword="false"/>.
        /// </returns>
        private bool TryFillBuffer(int minBytes)
        {
            int bytesUnread = this.readLength - this.readOffset;

            if (bytesUnread > 0)
            {
                Buffer.BlockCopy(this.buffer, this.readOffset, this.buffer, 0, bytesUnread);
            }

            this.readOffset = 0;
            this.readLength = bytesUnread;
            do
            {
                int bytesRead = this.stream.Read(this.buffer, this.readLength, this.bufferSize - this.readLength);

                if (bytesRead == 0)
                {
                    return false;
                }

                this.readLength += bytesRead;

            } while (this.readLength < minBytes);

            return true;
        }

        /// <summary>
        /// Verifies that the <see cref="EndianBinaryReader"/> has not been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        private void VerifyNotDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(EndianBinaryReader));
            }
        }
    }
}
