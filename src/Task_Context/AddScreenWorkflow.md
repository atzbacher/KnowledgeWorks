# The Add Screen Purpose 

The "Add Screen" is the view were users can add files (presentations, pdf, docs, notes) manually or semi-automatically to the database.


## GUI 
==Control panel==
"Add File/Folder" - Button
"Review Items" - Button
"Commit Selected" - Button
"Clear" - Button


==Watched Folder Panel== 
"Scan All" - Button
"Add Watched Folder" - Button

==Folder Table==
Rows: Enabled checkbox / Folder / Last Scan / Status / "Scan Now"-Button / "Remove"-Button


==Staging Table==
Rows: Selected checkbox / Title / Display Name / Authors / Year / Source / Tags / DOI / PMID / Similar to / Similarity / "Action"-DropDown / Internal

## Functions
==Control panel==
"Add File/Folder" - Button: Opens a browser window to select a file or folder (option for recursive) to ingest
"Review Items"-Button: Open the "Review Window" 
"Commit Selected": Adds all selected Items to the database respecting the "Action"-DropdDown selection

==Watched Folder Panel== 
"Scan All" - Button: Scans all folders that are displayed in the =Folder Table=
"Add Watched Folder" - Button: Adds a new path/folder that can be scanned either via the "Scan All"-Button or via the "Scan Now"-Button

==Folder Table==
Displays all folders that have been added by user to be scanned, data are displayed (row nameing = data content), the Status field displayes a colored (Changed - Red, Unchanged - Green), the status reflects the status of the files and whether or not they have been added i.e. It only changes form changed to unchanged if the changed files have been commited to the database

==Staging Table==
Displays a list of files to be reviewed before staging, 
Selected checkbox: the checkbox let's you choose between selected and unselected in one click. 
Title: the title of the File, based on the file name of the original file
Display Name: Lastname of Author - year - Title of file (not file name) 
Authors: Author or list of authors for publication
Source: Either journal for publications, or folder name for other files
Tags: Keywords, either from a pubmed entry or from the file, otherwise comma seperated list by user (autocomplete from existing tags), 
DOI: Publicaiton ID
PMID: Pubmed ID
Internal ID: a unique internal ID
Similar to: has based or identifier based similarity with files in the database and the current staging ready files (eg.to capture a dublicate in the same folder, or different version of the same file in the same scanned foler)
Similaity: % similarity between files
"Action"-DropDown: New -> new entry to database, Version -> Add to existing file (ask if user wants to replace file or add file as attachment or wants to move the existing file to the attachment adding the new one as firstline file), Variant -> Add as new entry with relation to the chosen base file, Add -> Adds to existing entry (asks for relation to first line file eg. Supplement) All operations here should let the user choose from existing files prepopulating with the most similar one but able to search the whole db and the current staging area (ie. loading a supplemnt at the same time as the main paper)

## Workflow

==Add Folder/File/Scan Folder Workflow==
User selects a file/folder or scans.
System load files async. Extracts metadata.
ifa DOI or PMID is found in the file/pdf metadata, the DOI is cleaned/normalized before searching pubmed, get the PMID and fetching all info from Pubmed.
If PMID is already in DB do not fetch any data, deselect it indicating that the file is loaded and does not need to be added, but leave it for review (it could be a variant) if similarity is not 100%, grey out only exact matches by PMID
If no PMID is found display metadata only and perform has based similarity matching
Once all items are reviewed and saved to the database the UI is cleared.
