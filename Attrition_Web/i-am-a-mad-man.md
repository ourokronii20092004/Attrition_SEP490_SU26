Backend can block these attacks:

Authentication & Credential Attacks
- Brute-Force Login Attacks
- Weak Password Exploitation
- Username and Email Enumeration
- Login Timing Oracles
- Password Theft at Rest
- Token Theft at Rest
- Stolen-Token Replay Attacks
- OAuth Account Takeover

Session, Token & Authorization Attacks
- XSS Token Exfiltration * Cross-Site Request Forgery (CSRF)
- Cookie Scope Leaks
- JWT Forgery and Tampering
- Insecure Direct Object Reference (IDOR) (Mitigated via strict service-layer ownership checks)

Injection, Content & File Attacks
- Stored Cross-Site Scripting (XSS) (Mitigated via AngleSharp-based HTML parsing)
- SQL Injection (SQLi)
- MIME/Content-Type Sniffing * Malicious File Uploads / Extension Spoofing (Mitigated via magic byte validation)
- Path Traversal / Directory Traversal
- Static Temp-File Disclosure

Abuse, DoS & Resource Attacks
- Global Request Flooding
- Oversized Upload Denial of Service (DoS)
- Unbounded Result Set / Memory Exhaustion (Mitigated via pageSize clamping)
- Null/Empty Request Body DoS (Mitigated via explicit rejection to prevent NRE crashes)

Data Integrity & Concurrency Attacks
- Time-of-Check to Time-of-Use (TOCTOU) Duplicate Races
- Partial Write / Incomplete Transaction Corruption
- Lost-Update Attacks on Counters
- Concurrent-Delete Races
- Dangling Reference Corruption (Mitigated via tombstoned deletions)

Service & Infrastructure Attacks
- Forged Internal Microservice Calls
- Public Exposure of Internal Endpoints
- Stack-Trace / Internal Information Disclosure