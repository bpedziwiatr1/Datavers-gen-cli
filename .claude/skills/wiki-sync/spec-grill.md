---
name: spec-grill
description: Analizuje projekt, prowadzi wywiad (Grill-Me) i tworzy plik specyfikacji w /specs/ w kontekście kodu.
---

# Skill: Spec Grill & Architecture Plan

Gdy użytkownik wywoła `/spec-grill <nazwa-funkcjonalnosci>`:

1. **Kontekst Projektu:** Przejrzyj istniejące pliki w projekcie (struktura folderów, istniejące DTO, serwisy, konwencje), aby zrozumieć, gdzie nowa funkcja się wpina.
2. **Tryb Grill-Me:** Zadawaj po 2-3 pytania na raz. Pytaj o:
   - Skrajne przypadki (edge cases) i obsługę błędów.
   - Integrację z obecną architekturą (jak ma rozszerzać istniejący kod).
   - Ograniczenia i wymagania (performance, formaty danych itp.).
3. **Plik Specyfikacji:** Po zebraniu ustaleń wygeneruj plik `specs/00X-<nazwa-funkcjonalnosci>.md` zawierający:
   - **Context & Goal:** Cel zmiany w kontekście obecnego repo.
   - **Architecture & Impact:** Diagram Mermaid i lista modyfikowanych/nowych klas.
   - **Acceptance Criteria:** Checklista wymagań i scenariuszy testowych.
4. **ZASADA:** Nie pisz kodu aplikacyjnego. Zatrzymaj się po stworzeniu pliku `.md`.