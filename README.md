These are files to simply find out the various functions of C#.

## OpcuaService
**OpcuaService** is a background service that keeps the OPC UA connection alive, enabling fast read and write operations from anywhere.  
- Checks the connection every **1 second**  
- Retries after **5 seconds** if disconnected  
- Automatically attempts to **reconnect** when read/write operations are made during disconnection
