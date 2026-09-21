# Vendored SENetworkAPI

Repository: https://github.com/Gauge/SENetworkAPI

Version: 2.0.0
Commit: `3a83159f63cad90916df351e801c2b8e17031544`

All six root C# files and `license.txt` are copied unchanged. Local integration lives outside this directory. Update all six files together; do not retain the previous local `CommandParser.cs` or `PacketCodec.cs`.

Upstream uses legacy, unauthenticated message handlers. Sender IDs are claimed by the packet, and transfer direction is not enforced on receipt. Remote admin authorization and settings synchronization therefore inherit upstream's documented trust limitation. See https://github.com/Gauge/SENetworkAPI/blob/3a83159f63cad90916df351e801c2b8e17031544/docs/known-issues.md .
