using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Aetherphone.Harness.Bootstrap;

internal static class PortableExecutablePatcher
{
    private const int PeSignatureOffsetField = 0x3C;
    private const ushort MachineAmd64 = 0x8664;
    private const ushort MachineArm64 = 0xAA64;
    private const ushort OptionalHeaderMagicPe32Plus = 0x20B;
    private const int OptionalHeaderStart = 24;
    private const int ClrDirectoryOffsetPe32Plus = 0xE0;
    private const uint CorFlagsIlOnly = 0x1;

    public static int PatchDirectoryToHost(string directory)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
        {
            return 0;
        }

        var files = Directory.GetFiles(directory, "*.dll");
        var patched = 0;
        for (var index = 0; index < files.Length; index++)
        {
            if (PatchToMachine(files[index], MachineArm64))
            {
                patched += 1;
            }
        }

        return patched;
    }

    private static bool PatchToMachine(string path, ushort machine)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 0x40)
        {
            return false;
        }

        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(PeSignatureOffsetField));
        if (peOffset <= 0 || peOffset + OptionalHeaderStart + 2 > data.Length)
        {
            return false;
        }

        var currentMachine = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(peOffset + 4));
        var magic = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(peOffset + OptionalHeaderStart));
        if (currentMachine != MachineAmd64 || magic != OptionalHeaderMagicPe32Plus || !IsIntermediateLanguageOnly(data, peOffset))
        {
            return false;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(peOffset + 4), machine);
        File.WriteAllBytes(path, data);
        Console.WriteLine($"Patched machine type of {Path.GetFileName(path)} for this host");
        return true;
    }

    private static bool IsIntermediateLanguageOnly(byte[] data, int peOffset)
    {
        var clrDirectory = peOffset + OptionalHeaderStart + ClrDirectoryOffsetPe32Plus;
        var clrRva = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(clrDirectory));
        if (clrRva == 0)
        {
            return false;
        }

        var clrHeaderOffset = RvaToFileOffset(data, peOffset, clrRva);
        if (clrHeaderOffset < 0)
        {
            return false;
        }

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(clrHeaderOffset + 16));
        return (flags & CorFlagsIlOnly) != 0;
    }

    private static int RvaToFileOffset(byte[] data, int peOffset, uint rva)
    {
        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(peOffset + 6));
        var optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(peOffset + 20));
        var sectionTable = peOffset + OptionalHeaderStart + optionalHeaderSize;
        for (var index = 0; index < sectionCount; index++)
        {
            var section = sectionTable + index * 40;
            var virtualSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(section + 8));
            var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(section + 12));
            var rawSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(section + 16));
            var rawPointer = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(section + 20));
            var span = Math.Max(virtualSize, rawSize);
            if (rva >= virtualAddress && rva < virtualAddress + span)
            {
                return (int)(rva - virtualAddress + rawPointer);
            }
        }

        return -1;
    }
}
