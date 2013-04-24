using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    class BinaryWriterEx : BinaryWriter
    {
        private bool ownStream;
        public BinaryWriterEx(Stream stream, bool ownStream) : base(stream)
        {
            this.ownStream = ownStream;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && ownStream)
            {
                base.OutStream.Dispose();
            }
        }

        public void Write(System.Drawing.Rectangle rect)
        {
            this.Write(rect.Left);
            this.Write(rect.Top);
            this.Write(rect.Right);
            this.Write(rect.Bottom);
        }
    }
}
