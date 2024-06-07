namespace ATRI.Collection
{
    using System;
    using System.IO;

    public class SubFileStream : Stream
    {
        private Stream _baseStream;
        private long _start;
        private long _length;
        private long _position;

        public SubFileStream(Stream baseStream, long start, long length)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _start = start;
            _length = length;
            _position = 0;

            if (!_baseStream.CanRead || !_baseStream.CanSeek)
            {
                throw new ArgumentException("The base stream must support reading and seeking.", nameof(baseStream));
            }

            if (start < 0 || length < 0 || start + length > _baseStream.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(start),
                    "The specified range is outside the bounds of the base stream.");
            }
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _length)
                {
                    throw new ArgumentOutOfRangeException(nameof(value),
                        "Position must be within the bounds of the sub stream.");
                }

                _position = value;
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException("SubFileStream does not support writing.");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count)
                throw new ArgumentException("Invalid offset and length.");

            if (_position >= _length)
                return 0;

            _baseStream.Position = _start + _position;
            int bytesRead = _baseStream.Read(buffer, offset, (int)Math.Min(count, _length - _position));
            _position += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = _position + offset;
                    break;
                case SeekOrigin.End:
                    newPosition = _length + offset;
                    break;
                default:
                    throw new ArgumentException("Invalid seek origin.", nameof(origin));
            }

            if (newPosition < 0 || newPosition > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    "Seek position is outside the bounds of the sub stream.");
            }

            _position = newPosition;
            return _position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("SubFileStream does not support setting length.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("SubFileStream does not support writing.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream?.Dispose();
                _baseStream = null;
            }

            base.Dispose(disposing);
        }
    }
}