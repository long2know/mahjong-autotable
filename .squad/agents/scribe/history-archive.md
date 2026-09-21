# Scribe history archive

The existing archive is preserved in order across these parts so each newly
committed file remains below the repository's 512 KiB limit:

1. [Part 1](history-archive-part-01.md)
2. [Part 2](history-archive-part-02.md)

Concatenating the parts with one additional newline between them reproduces
the original archive byte for byte. The separator keeps each individual file
compatible with the repository's end-of-file formatting hook.
Original SHA-256: `9cc34257a2681b82c3b7d83c0eb12194cefb4a08308801ddd9c654b0495ad162`.
