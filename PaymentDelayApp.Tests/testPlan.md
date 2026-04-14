# Test coverage registry (`PaymentDelayApp.Tests`)

Use this file before adding tests: find the **production type / member** below; if it already lists tests, extend those tests or add a new case in the same class unless the behavior is unrelated.

---

## `InvoiceDashboardImportService` (`PaymentDelayApp/Services/InvoiceDashboardImportService.cs`)

| Member | Test class | Test methods (indicative) |
|--------|------------|---------------------------|
| `ImportFromExcelAsync` | `InvoiceDashboardImportFromExcelTests` | `ImportFromExcelAsync_CancelledToken_ThrowsOperationCanceled`, `ImportFromExcelAsync_InvalidStream_Throws`, `ImportFromExcelAsync_NoWorksheets_HeaderFailureMissingSheet`, `ImportFromExcelAsync_HeaderNotFound_HeaderFailure`, `ImportFromExcelAsync_OnlyHeaderNoData_OkZero`, `ImportFromExcelAsync_ParseError_NoSave`, `ImportFromExcelAsync_MixedValidAndInvalid_NoSave`, `ImportFromExcelAsync_InFileDuplicate_NoSave`, `ImportFromExcelAsync_DatabaseDuplicate_NoSave`, `ImportFromExcelAsync_TwoValidRows_SavesInOrder`, `ImportFromExcelAsync_SecondSaveThrows_ExceptionPropagates`, `ImportFromExcelAsync_GetSuppliersThrows_Propagates` |
| `GetCellText` (internal static) | `InvoiceDashboardImportCellAndParserTests` | `GetCellText_EmptyCell_ReturnsEmpty`, `GetCellText_TrimsWhitespace`, `GetCellText_OnlySpaces_ReturnsEmpty` |
| `IsBlankDataRow` (internal static) | `InvoiceDashboardImportCellAndParserTests` | `IsBlankDataRow_AllNineEmpty_ReturnsTrue`, `IsBlankDataRow_AllNineDashDisplay_ReturnsTrue`, `IsBlankDataRow_MixEmptyAndDash_ReturnsTrue`, `IsBlankDataRow_OneNonEmpty_ReturnsFalse` |
| `TryParseDateOnly` (internal static) | `InvoiceDashboardImportCellAndParserTests` | `TryParseDateOnly_EmptyAfterTrim_ReturnsFalse`, `TryParseDateOnly_dd_MM_yyyy_Parses`, `TryParseDateOnly_d_M_yyyy_Parses`, `TryParseDateOnly_dd_M_yyyy_Parses`, `TryParseDateOnly_d_MM_yyyy_Parses`, `TryParseDateOnly_TrimsThenParses`, `TryParseDateOnly_Garbage_ReturnsFalse` |
| `TryParseDecimalFr` (internal static) | `InvoiceDashboardImportCellAndParserTests` | `TryParseDecimalFr_SpacesAndComma_Parses`, `TryParseDecimalFr_NbspAndSpaces_Parses`, `TryParseDecimalFr_InvariantDot_Parses`, `TryParseDecimalFr_EmptyOrLetters_ReturnsFalse` |
| `BuildSupplierLookup` (internal static) | `InvoiceDashboardImportLookupTests` | `BuildSupplierLookup_Empty_NoAmbiguous`, `BuildSupplierLookup_OneSupplier_MapsByNormalizedName`, `BuildSupplierLookup_TwoSuppliersSameNameCaseInsensitive_BothAmbiguousAndRemoved`, `BuildSupplierLookup_TwoSuppliersIdenticalTrimmedName_BothRemoved`, `BuildSupplierLookup_WhitespaceOnlyName_Skipped`, `BuildSupplierLookup_ThreeSuppliersA_a_B_OnlyBRemains` |
| `TryParseRow` (internal static) | `InvoiceDashboardImportTryParseRowTests` | `TryParseRow_InvoiceDateEmpty_ReturnsMessage`, `TryParseRow_InvoiceDateWrongYear_ReturnsYearMessage`, `TryParseRow_DeliveryEmpty_ContinuesWithNullDelivery`, `TryParseRow_DeliveryDash_TreatedAsNull`, `TryParseRow_DeliveryInvalid_ReturnsMessage`, `TryParseRow_DeliveryWrongYear_ReturnsYearMessage`, `TryParseRow_NumberEmpty_ReturnsMessage`, `TryParseRow_SupplierEmpty_ReturnsMessage`, `TryParseRow_SupplierAmbiguous_ReturnsMessage`, `TryParseRow_SupplierUnknown_ReturnsMessage`, `TryParseRow_TtcEmpty_ReturnsMessage`, `TryParseRow_TtcInvalid_ReturnsMessage`, `TryParseRow_EcheanceC7Empty_ReturnsMessage`, `TryParseRow_EcheanceC9Dash_ReturnsMessage`, `TryParseRow_EcheanceC7AndC9Mismatch_ReturnsMessage`, `TryParseRow_DerivedEcheanceTooShort_ReturnsRangeMessage`, `TryParseRow_DerivedEcheanceTooLong_ReturnsRangeMessage`, `TryParseRow_ResteJoursEmpty_ReturnsMessage`, `TryParseRow_ResteJoursMissingJ_ReturnsFormatMessage`, `TryParseRow_ResteJoursVariants_Accepted`, `TryParseRow_DesignationWhitespaceOnly_NullDesignation`, `TryParseRow_HappyPath_SetsInvoiceFields` |
| `AddInFileDuplicateErrors` (internal static) | `InvoiceDashboardImportDuplicateTests` | `AddInFileDuplicateErrors_EmptyParsed_NoChange`, `AddInFileDuplicateErrors_SingleRow_NoDuplicate`, `AddInFileDuplicateErrors_SameSupplierAndNumber_SecondRowErrorReferencesFirstExcelRow`, `AddInFileDuplicateErrors_UnsortedParsed_FirstByExcelRowWins`, `AddInFileDuplicateErrors_SameNumberDifferentSuppliers_NoInFileDuplicate` |
| `AddDatabaseDuplicateErrorsAsync` (internal) | `InvoiceDashboardImportDuplicateTests` | `AddDatabaseDuplicateErrorsAsync_NoneExist_NoErrors`, `AddDatabaseDuplicateErrorsAsync_OneExists_AddsRowError`, `AddDatabaseDuplicateErrorsAsync_CancelledToken_PropagatesOperationCanceled`, `AddDatabaseDuplicateErrorsAsync_AccessThrows_Propagates` |

**Not directly unit-tested (or only via `ImportFromExcelAsync`):** constructor wiring; `PaymentDelayDbContext` transaction **commit** vs **rollback** is exercised indirectly (real SQLite + `BeginTransactionAsync`); explicit `CommitAsync` / `RollbackAsync` call counts are not asserted.

**Types with no dedicated tests:** `InvoiceDashboardImportParsedRow` (record; covered indirectly through import / duplicate tests).

---

## `DashboardInvoiceExcelHeaderFinder` (`PaymentDelayApp/Services/DashboardInvoiceExcelHeaderFinder.cs`)

| Member | Test class | Test methods (indicative) |
|--------|------------|---------------------------|
| `FindHeaderRow` | `InvoiceDashboardImportServiceTests` | `FindHeaderRow_HeaderOnRow3_Returns3`, `FindHeaderRow_HeaderOnRow1_Returns1`, `FindHeaderRow_HeaderOnRow10_Returns10`, `FindHeaderRow_SkipsPartialRow_ReturnsFirstFullMatch`, `FindHeaderRow_HeaderOnlyOnRow11_ReturnsNull`, `FindHeaderRow_OneTypoInHeader_ReturnsNull`, `FindHeaderRow_WrongColumnOrder_ReturnsNull`, `FindHeaderRow_ExtraInnerSpace_ReturnsNull`, `FindHeaderRow_WrongAccentOnLivraison_ReturnsNull`, `FindHeaderRow_CaseMismatch_ReturnsNull`, `FindHeaderRow_LeadingTrailingSpacesOnCells_StillMatches`, `FindHeaderRow_EmptySheet_ReturnsNull`, `FindHeaderRow_TwoFullHeaderRows_ReturnsFirst` |

Note: this class has its **own** private `GetCellText`; it is not the same symbol as `InvoiceDashboardImportService.GetCellText`.

---

## `DatabaseBackupService` / `IBackupService` (`PaymentDelayApp/Services/DatabaseBackupService.cs`)

| Member | Test class | Test methods (indicative) |
|--------|------------|---------------------------|
| `CreateBackupAsync` | `DatabaseBackupServiceTests` | `CreateBackupAsync_SourceMissing_ThrowsFileNotFoundException`, `CreateBackupAsync_NullOrWhiteSpacePaths_ThrowArgumentException`, `CreateBackupAsync_WritesBackupFileAndCopiesData`, `CreateBackupAsync_PreCancelledToken_DoesNotWriteBackup`, `CreateBackupAsync_ThenPruneRemovesStaleBackups` |
| `PruneBackupsAsync` | `DatabaseBackupServiceTests` | `PruneBackupsAsync_MissingDirectory_CompletesWithoutThrow`, `PruneBackupsAsync_NullOrWhiteSpaceDirectory_ThrowsArgumentException`, `PruneBackupsAsync_DeletesOldAppBackupFilesOnly`, `PruneBackupsAsync_RetentionDaysClampedToMinimum`, `PruneBackupsAsync_CancelledToken_ThrowsOperationCanceled` |

**Also exercised indirectly:** `BackupSettingsFile.ClampRetentionDays` via retention arguments (e.g. `0` → minimum). No separate unit tests for `BackupSettingsFile` in this project.

---

## `InvoiceDashboardExportService` / `IInvoiceDashboardExportService` (`PaymentDelayApp/Services/InvoiceDashboardExportService.cs`)

| Member | Test class | Test methods (indicative) |
|--------|------------|---------------------------|
| `WriteExcelAsync` | `InvoiceDashboardExportServiceTests` | `WriteExcelAsync_WritesTitleStampHeadersAndRows`, `WriteExcelAsync_EmptyRows_StillWritesLayout`, `WriteExcelAsync_SanitizesInvalidSheetCharacters`, `WriteExcelAsync_OnlyInvalidTitleCharacters_UsesDefaultSheetName`, `WriteExcelAsync_LongTitle_TruncatesSheetNameTo31`, `WriteExcelAsync_PreCancelledToken_ThrowsOperationCanceled` |
| `WritePdfAsync` | `InvoiceDashboardExportServiceTests` | `WritePdfAsync_WritesPdfHeader`, `WritePdfAsync_EmptyRows_StillProducesPdf`, `WritePdfAsync_PreCancelledToken_ThrowsOperationCanceled` |

**Private (covered only via `WriteExcelAsync`):** `SanitizeSheetName` — invalid characters removed, empty → `"Factures"`, max length 31.

**Not separately asserted:** QuestPDF table cell text vs. row fields (beyond PDF magic / size); `InvoiceDashboardRow` display properties are exercised indirectly through Excel assertions.

---

## Shared test helpers

| Helper | Location | Role |
|--------|----------|------|
| `WriteHeaderRow`, `WriteDataCells`, `WorkbookToRewindableStream` | `InvoiceDashboardImportTestExcel` | ClosedXML layout + stream for import tests |
| `CreateXlsxStreamWithNoWorksheets` (minimal OPC zip) | `InvoiceDashboardImportFromExcelTests` (private) | Zero-sheet workbook stream for `ImportFromExcelAsync` |

---

## Test classes with **no** `[TestMethod]` yet (placeholders)

| Class | Intended production surface |
|-------|-----------------------------|
| `SupplierNameNormalizerTests` | `SupplierNameNormalizer` |
| `SupplierExcelServiceTests` | `SupplierExcelService` |

Add new tests there (or new `*Tests.cs` files) when you implement coverage; update this registry in the same PR.

---

## Quick “do not duplicate” checklist

Before adding a test for dashboard invoice import, search this file and the `InvoiceDashboardImport*.cs` test sources for the **production member name** (e.g. `TryParseRow`, `BuildSupplierLookup`, `ImportFromExcelAsync`). Prefer extending an existing test class unless the scenario is a new subsystem.

For **database backup** behavior, search `DatabaseBackupServiceTests` and the `CreateBackupAsync` / `PruneBackupsAsync` rows above.

For **dashboard export** (Excel / PDF), search `InvoiceDashboardExportServiceTests` and the `WriteExcelAsync` / `WritePdfAsync` rows above.
