using CNUS_packer.crypto;
using CNUS_packer.fst;
using CNUS_packer.packaging;
using CNUS_packer.utils;

using System;
using System.Collections.Generic;
using System.IO;

namespace CNUS_packer.contents
{
    public class Content
    {
        public static short TYPE_CONTENT = 8192;
        public static short TYPE_ENCRYPTED = 1;
        public static short TYPE_HASHED = 2;

        private int ID = 0;

        private short index = 0;

        private short type = (short)(TYPE_CONTENT & TYPE_ENCRYPTED);

        private long encryptedFileSize;

        private byte[] SHA2 = new byte[20];

        private long curFileOffset = 0;
        public static int ALIGNMENT_IN_CONTENT_FILE = 32;
        public static int CONTENT_FILE_PADDING = 32768;

        private List<FSTEntry> entries = new List<FSTEntry>();

        private int groupID = 0;

        private long parentTitleID = 0;

        private short entriesFlags = 0;

        private bool isFSTContent;

        public Content()
        {
        }

        public int getID()
        {
            return this.ID;
        }

        public void setID(int ID)
        {
            this.ID = ID;
        }

        public short getType()
        {
            return type;
        }

        public void addType(short type)
        {
            this.type |= type;
        }

        public void removeType(short type)
        {
            this.type &= (short)~type;
        }

        public void setType(short type)
        {
            this.type = type;
        }

        public short getIndex()
        {
            return index;
        }

        public void setIndex(short index)
        {
            this.index = index;
        }

        public long getParentTitleID()
        {
            return parentTitleID;
        }

        public void setParentTitleID(long parentTitleID)
        {
            this.parentTitleID = parentTitleID;
        }

        public int getGroupID()
        {
            return groupID;
        }

        public void setGroupID(int groupID)
        {
            this.groupID = groupID;
        }

        private long getCurFileOffset()
        {
            return curFileOffset;
        }

        private void setCurFileOffset(long curFileOffset)
        {
            this.curFileOffset = curFileOffset;
        }

        public void setEncryptedFileSize(long size)
        {
            this.encryptedFileSize = size;
        }

        public long getEncryptedFileSize()
        {
            return this.encryptedFileSize;
        }

        public void setHash(byte[] hash)
        {
            this.SHA2 = hash;
        }

        public byte[] getHash()
        {
            return this.SHA2;
        }

        public bool isHashed()
        {
            return (getType() & TYPE_HASHED) == TYPE_HASHED;
        }

        public bool getIsFSTContent()
        {
            return isFSTContent;
        }

        public void setFSTContent(bool isFSTContent)
        {
            this.isFSTContent = isFSTContent;
        }

        public void setEntriesFlags(short entriesFlag)
        {
            this.entriesFlags = entriesFlag;
        }

        public short getEntriesFlags()
        {
            return entriesFlags;
        }

        public int getFSTContentHeaderDataSize()
        {
            return 32;
        }

        public Pair<byte[],long> getFSTContentHeaderAsData(long old_content_offset)
        {
            MemoryStream buffer = new MemoryStream(getFSTContentHeaderDataSize());

            byte unkwn = 0;
            long content_offset = old_content_offset;
            long fst_content_size = getEncryptedFileSize() / CONTENT_FILE_PADDING;
            long fst_content_size_written = fst_content_size;

            if (isHashed())
            {
                unkwn = 2;
                fst_content_size_written -= ((fst_content_size / 64) + 1) * 2;
                if (fst_content_size_written < 0) fst_content_size_written = 0;
            }
            else
            {
                unkwn = 1;
            }

            if (getIsFSTContent())
            {
                unkwn = 0;
                if(fst_content_size == 1)
                {
                    fst_content_size = 0;
                }
                content_offset += fst_content_size + 2;
            }
            else
            {
                content_offset += fst_content_size;
            }

            // we need to write with big endian, so we'll Array.Reverse a lot
            byte[] temp;

            temp = BitConverter.GetBytes((int)old_content_offset);
            Array.Reverse(temp);
            buffer.Write(temp);

            temp = BitConverter.GetBytes((int)fst_content_size_written);
            Array.Reverse(temp);
            buffer.Write(temp);

            temp = BitConverter.GetBytes(getParentTitleID());
            Array.Reverse(temp);
            buffer.Write(temp);

            temp = BitConverter.GetBytes(getGroupID());
            Array.Reverse(temp);
            buffer.Write(temp);

            buffer.WriteByte(unkwn);

            return new Pair<byte[], long>(buffer.GetBuffer(), content_offset);
        }

        public long getOffsetForFileAndIncrease(FSTEntry fstEntry)
        {
            long old_fileoffset = getCurFileOffset();
            setCurFileOffset(old_fileoffset + utils.utils.align(fstEntry.getFilesize(), ALIGNMENT_IN_CONTENT_FILE));
            return old_fileoffset;
        }

        public void resetFileOffsets()
        {
            curFileOffset = 0;
        }

        private List<FSTEntry> getFSTEntries()
        {
            return entries;
        }

        public int getFSTEntryNumber()
        {
            return entries.Count;
        }

        public byte[] getAsData()
        {
            MemoryStream buffer = new MemoryStream(getDataSize());

            // We need to write in big endian, so we're gonna Array.Reverse a lot
            byte[] temp;

            temp = BitConverter.GetBytes(getID());
            Array.Reverse(temp);
            buffer.Write(temp);

            temp = BitConverter.GetBytes(getIndex());
            Array.Reverse(temp);
            buffer.Write(temp);

            temp = BitConverter.GetBytes(getType());
            Array.Reverse(temp);
            buffer.Write(temp);

            temp = BitConverter.GetBytes(getEncryptedFileSize());
            Array.Reverse(temp);
            buffer.Write(temp);

            buffer.Write(getHash());

            Console.WriteLine("all: " + getID() + ", " + getIndex() + ", " + getType() + ", " + getEncryptedFileSize() + ", " + System.Text.Encoding.Default.GetString(getHash()));
            Console.WriteLine("Content.getAsData(): " + System.Text.Encoding.Default.GetString(buffer.GetBuffer()));

            return buffer.GetBuffer();
        }

        public int getDataSize()
        {
            return 48;
        }

        public void packContentToFile(string outputDir)
        {
            Console.WriteLine("Packing Content " + getID().ToString("X8") +"\n");

            NUSpackage nusPackage = NUSPackageFactory.getPackageByContent(this);
            Encryption encryption = nusPackage.getEncryption();
            Console.WriteLine("Packing files into one file:");
            //At first we need to create the decrypted file.
            FileInfo decryptedFile = packDecrypted();

            Console.WriteLine();
            Console.WriteLine("Generate hashes:");
            //Calculates the hashes for the decrypted content. If the content is not hashed,
            //only the hash of the decrypted file will be calculated

            ContentHashes contentHashes = new ContentHashes(decryptedFile, isHashed());

            string h3_path = Path.Combine(outputDir, getID().ToString("X8") + ".h3");

            contentHashes.saveH3ToFile(h3_path);
            setHash(contentHashes.getTMDHash());
            Console.WriteLine();
            Console.WriteLine("Encrypt content (" + getID().ToString("X8") + ")");
            FileInfo encryptedFile = packEncrypted(outputDir, decryptedFile, contentHashes, encryption);

            setEncryptedFileSize(encryptedFile.Length);

            Console.WriteLine();
            Console.WriteLine("Content " + getID().ToString("X8") + " packed!");
            Console.WriteLine("-------------");
        }

        private FileInfo packEncrypted(string outputDir, FileInfo decryptedFile, ContentHashes hashes, Encryption encryption)
        {
            string outputFilePath = Path.Combine(outputDir, getID().ToString("X8") + ".app");
            if((getType() & TYPE_HASHED) == TYPE_HASHED)
            {
                encryption.encryptFileHashed(decryptedFile, this, outputFilePath, hashes);
            }
            else
            {
                encryption.encryptFileWithPadding(decryptedFile, this, outputFilePath, CONTENT_FILE_PADDING);
            }

            return new FileInfo(Path.GetFullPath(outputFilePath));
        }

        private FileInfo packDecrypted()
        {
            string tmp_path = Path.Combine(settings.tmpDir, getID().ToString("X8") + ".dec");
            using (FileStream fos = new FileStream(tmp_path, FileMode.Create))
            {
                int totalCount = getFSTEntryNumber();
                int cnt_file = 1;
                long cur_offset = 0;
                foreach (FSTEntry entry in getFSTEntries())
                {
                    if (!entry.isNotInPackage())
                    {
                        if (entry.isFile())
                        {
                            if (cur_offset != entry.getFileOffset())
                            {
                                Console.WriteLine("FAILED");
                            }
                            long old_offset = cur_offset;
                            cur_offset += utils.utils.align(entry.getFilesize(), ALIGNMENT_IN_CONTENT_FILE);
                            string output = "[" + cnt_file + "/" + totalCount + "] Writing at " + old_offset + " | FileSize: " + entry.getFilesize() + " | " + entry.getFilename();

                            utils.utils.copyFileInto(entry.getFile(), fos, output);

                            int padding = (int)(cur_offset - (old_offset + entry.getFilesize()));
                            fos.Write(new byte[padding]);
                        }
                        else
                        {
                            Console.WriteLine("[" + cnt_file + "/" + totalCount + "] Wrote folder: \"" + entry.getFilename() + "\"");
                        }
                    }
                    cnt_file++;
                }
            }

            return new FileInfo(Path.GetFullPath(tmp_path));
        }

        public void update(List<FSTEntry> entries)
        {
            if(entries != null)
            {
                this.entries = entries;
            }
        }

        public bool equals(Object other)
        {
            bool result;
            if((other == null) || (GetType() != other.GetType()))
            {
                result = false;
            }
            else
            {
                Content other_ = (Content)other;
                result = ID == other_.ID;
            }
            return result;
        }
    }
}
