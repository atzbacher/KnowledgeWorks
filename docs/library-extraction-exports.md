# Library extraction exports

Library entries with committed data extraction hooks expose export commands directly from the **Library** results grid. Right-click an entry and open the **Export extraction** menu to generate:

- **PowerPoint (.pptx)** slides summarizing each extracted table and figure. Slides include entry metadata plus DOI, PMID, pages, and asset provenance hashes directly in the bullet text.
- **Word (.docx)** documents containing formatted tables populated from the stored CSV sources. Each section includes linked endpoint/intervention summaries and the recorded provenance hash.
- **Excel (.xlsx)** workbooks with two sheets: endpoints and baseline tables. The sheets reproduce structured data and prepend header rows that capture extractor, timestamp, DOI, PMID, and provenance hash details.

Commands are enabled only when the selected entry has a data extraction hook. The exporters automatically choose a default filename using the entry title; adjust the filename or location as needed in the save dialog before confirming.
