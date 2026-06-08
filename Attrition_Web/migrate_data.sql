INSERT INTO identity."Users" (
    "Id", "Username", "Email", "DisplayName", "PasswordHash", "Role", "JoinedAt", "UpdatedAt", "IsBanned", "IsDeleted", "IsEmailVerified", "MustChangePassword", "FailedLoginAttempts", "PostCount", "ContributionCount", "NotifyOnReply", "NotifyOnMention", "AuthProvider", "ThemeMode", "ThemeAccent"
) VALUES 
('11111111-1111-1111-1111-111111111111', 'PlayerOne', 'player1@example.com', 'PlayerOne', '$2a$11$fVGlQE.5SSeR.gdw7BUSG.4uFjPY4fCyubN02.DwwZGSC830zLktm', 'player', '2026-03-21 10:35:36.322582+07', '2026-03-21 10:35:36.322582+07', false, false, false, false, 0, 0, 0, true, true, 'Local', 'system', 'blue'),
('22222222-2222-2222-2222-222222222222', 'PlayerTwo', 'player2@example.com', 'PlayerTwo', '$2a$11$fVGlQE.5SSeR.gdw7BUSG.4uFjPY4fCyubN02.DwwZGSC830zLktm', 'player', '2026-03-21 10:35:36.322582+07', '2026-03-21 10:35:36.322582+07', false, false, false, false, 0, 0, 0, true, true, 'Local', 'system', 'blue'),
('9a06a007-7432-4ba5-b88f-5f2d29024171', 'DangNN', 'dangnnce180010@fpt.edu.vn', 'DangNN', '$2a$11$AmBP5krWBKNKL23tBnTHZuu.9ypuuBV3HTrsyDJVrjsk8WgHNAPES', 'player', '2026-03-22 08:18:58.369867+07', '2026-05-30 10:33:22.42053+07', false, false, false, false, 0, 0, 0, true, true, 'Local', 'system', 'blue'),
('00e9cdb9-a6fb-49c6-8e5b-946f0f98199d', 'BinhPP', 'binh123@gmail.com', 'BinhPP', '$2a$11$e1yzz5/6To8uN7D3jXfIvOxqE83gWWcIfvHza3AZ3Qatdy79csHTa', 'player', '2026-03-22 08:22:24.05334+07', '2026-06-05 16:41:55.266693+07', false, false, false, false, 0, 0, 0, true, true, 'Local', 'system', 'blue')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO character."characters" (
    "Id", "OwnerId", "Name", "Archetype", "CreatedAt", "UpdatedAt"
) VALUES 
('33333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', 'Knight_P1', 'Knight', '2026-03-21 10:35:36.322582+07', '2026-03-21 10:35:36.322582+07'),
('44444444-4444-4444-4444-444444444444', '22222222-2222-2222-2222-222222222222', 'Mage_P2', 'Mage', '2026-03-21 10:35:36.322582+07', '2026-03-21 10:35:36.322582+07'),
('f5ec6005-f2c8-4a01-a636-00a19cb6c9a7', '9a06a007-7432-4ba5-b88f-5f2d29024171', 'CHUDANGBODOi', 'Unknown', '2026-03-22 08:19:55.024986+07', '2026-03-22 08:19:55.024986+07'),
('a4e758c1-803a-4923-bb69-edbdb9bec749', '00e9cdb9-a6fb-49c6-8e5b-946f0f98199d', 'BinhBeoGay', 'Unknown', '2026-03-22 08:25:14.592916+07', '2026-03-22 08:25:14.592916+07')
ON CONFLICT ("Id") DO NOTHING;
