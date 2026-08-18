import sys
import struct

def patch_stack_size(exe_path, new_stack_size):
    with open(exe_path, 'r+b') as f:
        # Read DOS header to find PE header offset
        f.seek(0x3C)
        pe_offset_bytes = f.read(4)
        pe_offset = struct.unpack('<I', pe_offset_bytes)[0]
        
        # Verify PE signature
        f.seek(pe_offset)
        signature = f.read(4)
        if signature != b'PE\0\0':
            print("Not a valid PE file!")
            return False
            
        # COFF File Header is 20 bytes
        # Read optional header magic to determine if 32-bit or 64-bit
        f.seek(pe_offset + 24)
        magic = f.read(2)
        if magic == b'\x0b\x01':
            is_64bit = False
            stack_reserve_offset = pe_offset + 24 + 72
        elif magic == b'\x0b\x02':
            is_64bit = True
            stack_reserve_offset = pe_offset + 24 + 72
        else:
            print("Unknown PE format!")
            return False
            
        # Read current stack reserve
        f.seek(stack_reserve_offset)
        if is_64bit:
            current_stack = struct.unpack('<Q', f.read(8))[0]
            print(f"Current stack reserve: {current_stack} bytes")
            f.seek(stack_reserve_offset)
            f.write(struct.pack('<Q', new_stack_size))
        else:
            current_stack = struct.unpack('<I', f.read(4))[0]
            print(f"Current stack reserve: {current_stack} bytes")
            f.seek(stack_reserve_offset)
            f.write(struct.pack('<I', new_stack_size))
            
        print(f"Successfully patched stack reserve to: {new_stack_size} bytes")
        return True

if __name__ == '__main__':
    exe = r"C:\Users\rotem\OneDrive\Desktop\tilemaker-3.1.0\build\RelWithDebInfo\tilemaker.exe"
    patch_stack_size(exe, 16 * 1024 * 1024) # 16 MB stack
