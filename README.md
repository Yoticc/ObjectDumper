ObjectDumper
------------

Library for dumping objects and structures in the console.

Hardcoded for .net9. To support other versions, a slight update of the EEClass offset is required.

It is mainly a PoC and a repository for training in working with the internal structures of the VM. \
Main areas: object pinning, implementation of utf8 vt100 terminal, work with internal VM type structures.

Also, in addition to internal vm structures, reflection is used in the code. \
This is not necessary, but adding metadata processing can limit the dotnet version  \
from a major version to a minor one, so reflection was used for simplicity. \
Otherwise, you need to add structures: Module, PEAssembly, MDImport and its vtable. \

This could also serve as a better version [of this library](https://github.com/Yellow-Dog-Man/Verify.Marshaling)

Sample
------
<img width="1079" height="1016" alt="{E22B1DED-645C-4EA0-A661-CA0B377DB09D}" src="https://github.com/user-attachments/assets/a4f14034-21b0-42d8-8c20-8ebb28c3eac7" />

<img width="856" height="553" alt="{A5E1707C-4EA9-4F81-9F2D-DE89706F2AA8}" src="https://github.com/user-attachments/assets/0be3f5e7-3c3b-426a-ad68-2b10f94d3611" />
