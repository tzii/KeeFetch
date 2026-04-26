# Availability-First Reliability Regression Coverage

This document summarizes the new regression-oriented test coverage added for the reliability redesign.

## New tests

- `KeeFetch.Tests/IconSelectorTests.cs`
  - Tier 1 beats Tier 2
  - Tier 2 beats Tier 3
  - synthetic candidate rejected when stronger candidate exists
  - synthetic candidate allowed only as last resort
  - synthetic candidate rejected when synthetic fallback setting is disabled

- `KeeFetch.Tests/ProviderCapabilitiesTests.cs`
  - Provider tier assignments
  - Synthetic-capable provider classification
  - Per-provider concurrency cap expectations

- `KeeFetch.Tests/RegressionCorpusTests.cs`
  - Ensures issue #1 URL corpus fixture remains present and complete

## Expanded tests

- `KeeFetch.Tests/DirectSiteProviderTests.cs`
  - manifest link discovery
  - manifest icon parsing + relative URL resolution
  - SVG manifest icon signaling
  - `og:image` fallback deprioritization behavior

- `KeeFetch.Tests/ConfigurationTests.cs`
  - coverage for synthetic fallback toggle, provider flags/order, helper methods

- `KeeFetch.Tests/UtilTests.cs`
  - normalized origin key behavior (`scheme://host:port`)
  - HTTP URI parsing helpers used by cache identity logic

## Regression corpus fixture

- `KeeFetch.Tests/Fixtures/Issue1RegressionUrls.txt`
  - Contains the issue #1 problem URL set (skipped/wrong/placeholder cases) as the long-term corpus source.
