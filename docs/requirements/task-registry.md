# ArchPulse Task Registry — Notion snapshot

> [Source: Notion Engineering Work Breakdown](https://app.notion.com/p/5f2c39784dc24174a8989488350dd0fa). An index, not evidence of implemented work. Revalidate statuses before updates. GitHub issue creation does not complete tasks.

| Task | Title | Area | Gate | Priority | Notion status | Dependencies | Specification |
|---|---|---|---|---|---|---|---|
| [T001](https://app.notion.com/p/4143830ba34d817797f0e03b0551a6bb) | تحويل النطاق الشامل إلى Requirements Registry | Foundation | Architecture | P0 | Planned | — | S00 |
| [T002](https://app.notion.com/p/4143830ba34d81cfb87ac90959204533) | اعتماد مخطط السياقات والعقود الداخلية | Foundation | Architecture | P0 | Planned | T001 | S01 |
| [T003](https://app.notion.com/p/4143830ba34d81888c56d135862b55aa) | إنشاء سجلات ADR والتغييرات المعمارية | Foundation | Architecture | P0 | Planned | T001 | S22 |
| [T004](https://app.notion.com/p/4143830ba34d812699fcd53ed1ff4803) | بحث المنافسين وإثبات فرضية التميز AET | Foundation | Architecture | P0 | Planned | T001 | S21 |
| [T005](https://app.notion.com/p/4143830ba34d81999e32c321bdd8e901) | تأسيس Solution modular monolith .NET وإدارة البناء | Foundation | Core | P0 | Planned | T002 | S01 |
| [T006](https://app.notion.com/p/4143830ba34d81cab611d558753b259c) | تعريف نماذج Domain Events وAPI contracts | Foundation | Core | P0 | Planned | T002 | S14 |
| [T007](https://app.notion.com/p/4143830ba34d81e78a0bee2eed6e0710) | تصميم Plugin SDK ومحاكاة مكون خارجي | Foundation | Core | P0 | Planned | T005 | S24 |
| [T008](https://app.notion.com/p/4143830ba34d81878b7ccd2d01c8730c) | تصميم تخزين محلي SQLite ونموذج النسخ والهجرة | Foundation | Core | P0 | Planned | T006 | S15 |
| [T009](https://app.notion.com/p/4143830ba34d81b3b4cce70a8c4cd821) | تعريف عقد وحواف Graph والمعرفات المستقرة | Graph | Core | P0 | Planned | T006,T008 | S02 |
| [T010](https://app.notion.com/p/4143830ba34d817ea65dcea138227d8d) | تنفيذ Event-Sourced Graph Projection وإعادة البناء | Graph | Core | P0 | Planned | T009 | S02 |
| [T011](https://app.notion.com/p/4143830ba34d814f893eec39bf262b13) | الاستعلامات والتوسع المتدرج في Graph | Graph | Core | P0 | Planned | T010 | S02 |
| [T012](https://app.notion.com/p/4143830ba34d8111942fce19620a8497) | تتبع أصل الدليل وحدود الثقة | Graph | Core | P0 | Planned | T010 | S02 |
| [T013](https://app.notion.com/p/4143830ba34d811aaa9ff7a5350f8dfe) | تحميل حلول .NET مع Roslyn وMSBuildWorkspace | Graph | Core | P0 | Planned | T005,T009 | S03 |
| [T014](https://app.notion.com/p/4143830ba34d811baba9d0fe4d1a93be) | استخراج الرموز والعلاقات الدلالية C# | Graph | Core | P0 | Planned | T013 | S03 |
| [T015](https://app.notion.com/p/4143830ba34d81888fcdd4299342bca3) | الفهرسة التدريجية وWatchers وGit diff | Graph | Core | P0 | Planned | T014,T010 | S03 |
| [T016](https://app.notion.com/p/4143830ba34d814fbf0cfbc98e67a9d4) | محللات متعددة اللغات قابلة للإضافة | Graph | Integrated | P1 | Planned | T007,T014 | S03 |
| [T017](https://app.notion.com/p/4143830ba34d81eb8a30ef9cc477edca) | قابلية توسع Graph لمشروعات ضخمة | Graph | Production | P0 | Planned | T015,T011 | S02 |
| [T018](https://app.notion.com/p/4143830ba34d8128acaae0b490ee6a5c) | تصميم Rule DSL وقواعد طبقات ومجالات | Guardian | Core | P0 | Planned | T009,T014 | S04 |
| [T019](https://app.notion.com/p/4143830ba34d81c78a1bf0a775a6815f) | محرك تقييم Guardian وأدلة المخالفات | Guardian | Integrated | P0 | Planned | T018,T012 | S04 |
| [T020](https://app.notion.com/p/4143830ba34d817c9ac5c3a377c62477) | تشغيل قواعد المعمارية مع حفظ الملفات وCI | Guardian | Integrated | P0 | Planned | T019,T015 | S04 |
| [T021](https://app.notion.com/p/4143830ba34d811d98cdca05c376e63d) | إدارة Waivers واعتماد تصحيح التصميم | Guardian | Integrated | P0 | Planned | T019 | S04 |
| [T022](https://app.notion.com/p/4143830ba34d81d0984bffa90a01a4d3) | تاريخ الانحراف المعماري عبر الإصدارات | Guardian | Production | P1 | Planned | T020,T008 | S04 |
| [T023](https://app.notion.com/p/4143830ba34d8194bbc8e004221e3f77) | تصميم نموذج BusinessFeature/UseCase | Features | Integrated | P0 | Planned | T014,T012 | S05 |
| [T024](https://app.notion.com/p/4143830ba34d81e98370d11a764da0c6) | اكتشاف الوظائف من APIs وHandlers وTests | Features | Integrated | P0 | Planned | T023 | S05 |
| [T025](https://app.notion.com/p/4143830ba34d813b9f70f736bdcecd07) | ربط FeatureAtlas بالمخالفات وأثر PR | Features | Integrated | P0 | Planned | T024,T019 | S05 |
| [T026](https://app.notion.com/p/4143830ba34d816fa950d2a0baaced88) | مراجعة وتثبيت وتصحيح خرائط الوظائف | Features | Production | P1 | Planned | T025 | S05 |
| [T027](https://app.notion.com/p/4143830ba34d8196a273f6dccadea86a) | تحليل تدفق بيانات C# داخل الدوال | DataFlow | Integrated | P0 | Planned | T014,T012 | S06 |
| [T028](https://app.notion.com/p/4143830ba34d81c1ad70f6c6421f012d) | تتبع DataJourney بين الخدمات وEF والرسائل | DataFlow | Integrated | P0 | Planned | T027 | S06 |
| [T029](https://app.notion.com/p/4143830ba34d812dbe2aef92b872f8a2) | تصنيف البيانات الحساسة وسياسات Source/Sink | DataFlow | Production | P0 | Planned | T028 | S06 |
| [T030](https://app.notion.com/p/4143830ba34d81dca435d73848498346) | واجهة مسارات DataJourney والأدلة | DataFlow | Integrated | P1 | Planned | T028 | S06 |
| [T031](https://app.notion.com/p/4143830ba34d81598397e780adce7ed0) | اختبارات خصوصية واستعلامات SQL الديناميكية | DataFlow | Production | P1 | Planned | T029 | S06 |
| [T032](https://app.notion.com/p/4143830ba34d813481d4f438a2b63ec9) | Backup/Restore وRetention وتصدير المشروع | Foundation | Production | P1 | Planned | T008,T010 | S15 |
| [T033](https://app.notion.com/p/4143830ba34d81a7b72ff2224879d78c) | إدخال OTLP وتخزين Span/Metric/Log | Runtime | Integrated | P0 | Planned | T006,T008 | S08 |
| [T034](https://app.notion.com/p/4143830ba34d8148b0f4e0c9a7d177a7) | مطابقة Runtime Span مع الرموز وإصدارات البناء | Runtime | Integrated | P0 | Planned | T033,T012 | S08 |
| [T035](https://app.notion.com/p/4143830ba34d8149b2abcbc558e3280b) | قياسات النظام وخريطة الخدمات الفورية | Runtime | Integrated | P0 | Planned | T033 | S08 |
| [T036](https://app.notion.com/p/4143830ba34d81cf9f44c250bbb74130) | حماية المراقبة والتكلفة والتحكم في Sampling | Runtime | Production | P0 | Planned | T033,T035 | S08 |
| [T037](https://app.notion.com/p/4143830ba34d81d486baec3fec624c85) | Instrumentation للدوال المختارة مع Budget | Runtime | Production | P1 | Planned | T034 | S08 |
| [T038](https://app.notion.com/p/4143830ba34d81c7a735d13f811ea038) | واجهة Graph Canvas متعددة الطبقات | Studio | Integrated | P0 | Planned | T011,T012 | S09 |
| [T039](https://app.notion.com/p/4143830ba34d81fab94bf91364e3e894) | تحديثات Live للمخطط SignalR واستعادة الفجوات | Studio | Integrated | P0 | Planned | T038,T015 | S09 |
| [T040](https://app.notion.com/p/4143830ba34d8156ba2ae415beb568db) | واجهة عرض المخالفات وأدلة المصدر | Studio | Integrated | P0 | Planned | T038,T019 | S09 |
| [T041](https://app.notion.com/p/4143830ba34d810cb16dc96e4357c51c) | واجهة FeatureAtlas وDataJourney موحدة | Studio | Integrated | P0 | Planned | T038,T025,T030 | S09 |
| [T042](https://app.notion.com/p/4143830ba34d811f835fd2e5e8cc83ab) | واجهة تشغيل حي وربط Timeline بالرسم | Studio | Integrated | P0 | Planned | T038,T035 | S09 |
| [T043](https://app.notion.com/p/4143830ba34d8111a1bcddafe9cdaf00) | تحسين الوصولية والأداء وتجربة الهاتف | Studio | Production | P0 | Planned | T038,T039 | S09 |
| [T044](https://app.notion.com/p/4143830ba34d8127b76bc7a30c337e86) | إضافة VS Code تربط بالمحرك نفسه | Studio | Ecosystem | P0 | Planned | T038,T047 | S23 |
| [T045](https://app.notion.com/p/4143830ba34d81b5b2c8c06bab08a3d8) | عميل CLI متكامل للأتمتة | Studio | Ecosystem | P0 | Planned | T038,T047 | S23 |
| [T046](https://app.notion.com/p/4143830ba34d81fc9344fd3835fc7418) | روابط ويب آمنة ومتابعة VPS من الهاتف | Studio | Production | P1 | Planned | T039,T044 | S23 |
| [T047](https://app.notion.com/p/4143830ba34d81108af2e0d282f5d100) | [ASP.NET](http://ASP.NET) Core API versioning + Auth Gate | Agents | Core | P0 | Planned | T006,T008 | S14 |
| [T048](https://app.notion.com/p/4143830ba34d8160b81cd76bf5fb3fbe) | MCP Server للاستعلام الآمن عن المعمارية | Agents | Integrated | P0 | Planned | T047,T012 | S10 |
| [T049](https://app.notion.com/p/4143830ba34d81979e91c925f644840c) | تكامل Codex ومتابعة الأحداث وجلسات المهام | Agents | Integrated | P0 | Planned | T048,T047 | S10 |
| [T050](https://app.notion.com/p/4143830ba34d81a5b650ece8043ba1dd) | Agent Adapter محايد لمزود غير Codex | Agents | Integrated | P0 | Planned | T048 | S10 |
| [T051](https://app.notion.com/p/4143830ba34d81239f1cf4cdfdce13b1) | سياسة أذونات وأوامر الوكيل وموافقات الإنسان | Agents | Production | P0 | Planned | T049,T050 | S10 |
| [T052](https://app.notion.com/p/4143830ba34d81239ab0cc70c77f2306) | سجل نشاط الوكلاء متعدد المزودين | Agents | Production | P1 | Planned | T050,T047 | S10 |
| [T053](https://app.notion.com/p/4143830ba34d813f832dc77c81b617e3) | تثبيت Handoff Contract وعقود TaskEnvelope | Continuity | Architecture | P0 | Planned | T001,T002 | S11 |
| [T054](https://app.notion.com/p/4143830ba34d811c9a82ea435e816f6b) | Durable Task Ledger وCheckpoint immutable | Continuity | Core | P0 | Planned | T053,T008 | S11 |
| [T055](https://app.notion.com/p/4143830ba34d81b0bfb8fba60925f6b1) | Lease CAS + fencing token لمنع تنازع الوكلاء | Continuity | Core | P0 | Planned | T054 | S11 |
| [T056](https://app.notion.com/p/4143830ba34d81f28ef9e756e9185155) | Artifact manifests وEvidence Receipts لاستمرار العمل | Continuity | Core | P0 | Planned | T054 | S11 |
| [T057](https://app.notion.com/p/4143830ba34d81cf8979e763c2e5f16f) | Handoff package مستقل عن مزود AI | Continuity | Integrated | P0 | Planned | T055,T056 | S11 |
| [T058](https://app.notion.com/p/4143830ba34d81c48608eb055816076f) | Recovery coordinator وفحص آثار العمليات غير المؤكدة | Continuity | Integrated | P0 | Planned | T057,T047 | S11 |
| [T059](https://app.notion.com/p/4143830ba34d81dc832ef689bfe04c44) | Human-in-loop على RecoveryRequired وتعارض القرارات | Continuity | Integrated | P0 | Planned | T058,T051 | S11 |
| [T060](https://app.notion.com/p/4143830ba34d81358613f63540f2d4f9) | Crash-fault & split-brain conformance suite | Continuity | Production | P0 | Planned | T055,T058 | S11 |
| [T061](https://app.notion.com/p/4143830ba34d815bbb94f30cf001b70f) | استئناف تبادلي فعلي عبر وكيلين مختلفين | Continuity | Production | P0 | Planned | T057,T050 | S11 |
| [T062](https://app.notion.com/p/4143830ba34d81979a3fc5a4e4faefab) | لوحة استمرارية الوكلاء ووقت الاستعادة | Continuity | Production | P0 | Planned | T059,T039 | S11 |
| [T063](https://app.notion.com/p/4143830ba34d81fabdecce0cf26bd8d4) | تعويض الآثار والفشل الجزئي في external APIs | Continuity | Production | P0 | Planned | T058,T056 | S11 |
| [T064](https://app.notion.com/p/4143830ba34d819c8a37cffacfcaf512) | تعريف سيناريوهات RepoLab واكتشاف الاختبارات | RepoLab | Integrated | P0 | Planned | T014,T047 | S07 |
| [T065](https://app.notion.com/p/4143830ba34d8130bd18d2d8235ca09d) | تجهيز Worktree وSandbox Job Manifest | RepoLab | Integrated | P0 | Planned | T064,T051 | S13 |
| [T066](https://app.notion.com/p/4143830ba34d81f28f06ea8674a925b6) | تنفيذ تشغيل الاختبارات داخل Runner معزول | RepoLab | Integrated | P0 | Planned | T065 | S07 |
| [T067](https://app.notion.com/p/4143830ba34d81b48f2dd0b937323438) | Timeline تفاعلي لمسار التنفيذ والاختبارات | RepoLab | Integrated | P0 | Planned | T066,T042 | S07 |
| [T068](https://app.notion.com/p/4143830ba34d8151ae30d44556706bcc) | إعادة تشغيل السيناريو وتوثيق الانحرافات | RepoLab | Production | P0 | Planned | T066 | S07 |
| [T069](https://app.notion.com/p/4143830ba34d8138ba42da6777c1d8f7) | استئناف اختبارات توقفت باستلام وكيل جديد | RepoLab | Production | P1 | Planned | T066,T058 | S07 |
| [T070](https://app.notion.com/p/4143830ba34d817a831df9940ed34aa7) | تعريف Formal AET schema والحالات والانتقالات | Futures | Architecture | P0 | Planned | T004,T009 | S12 |
| [T071](https://app.notion.com/p/4143830ba34d810b99e8d4eed934b9bb) | حفظ معاملة تجربة معمارية وإعادة بناء حالتها | Futures | Core | P0 | Planned | T070,T054 | S12 |
| [T072](https://app.notion.com/p/4143830ba34d81a8b616e739e3caab4a) | بناء Candidate عبر وكيل داخل Git Worktree مستقل | Futures | Integrated | P0 | Planned | T071,T049,T050 | S12 |
| [T073](https://app.notion.com/p/4143830ba34d81c693e0ded5b5d87aa9) | تعريف Workload عادلة وقيود المقارنة | Futures | Integrated | P0 | Planned | T072,T066 | S12 |
| [T074](https://app.notion.com/p/4143830ba34d816e9415e86a81888d9c) | تجميع نتائج Benchmarks واختبارات Guardian للبدائل | Futures | Integrated | P0 | Planned | T073,T033 | S12 |
| [T075](https://app.notion.com/p/4143830ba34d817ebf19d6b0038b9514) | Visual Branch Comparison وFutureLab UI | Futures | Integrated | P0 | Planned | T074,T038 | S12 |
| [T076](https://app.notion.com/p/4143830ba34d819ba2c4c6f1ba7a27d9) | قرار اعتماد البديل ودمجه ببوابة مراجعة | Futures | Production | P0 | Planned | T075,T051 | S12 |
| [T077](https://app.notion.com/p/4143830ba34d813e9a67c708f5803de6) | Resumable AET بعد توقف العامل أو الوكيل | Futures | Production | P0 | Planned | T058,T071,T072 | S12 |
| [T078](https://app.notion.com/p/4143830ba34d8167b509c75afdb0ae75) | إثبات فائدة المنتج بتجربة مقارنة قابلة لإعادة الإنتاج | Futures | Production | P0 | Planned | T074,T075 | S12 |
| [T079](https://app.notion.com/p/4143830ba34d817aa49ed3edf9fdb4d1) | مراقبة التصميم المختار بعد النشر | Futures | Production | P1 | Planned | T076,T034 | S12 |
| [T080](https://app.notion.com/p/4143830ba34d81069d52fd0624573372) | إجراء Threat Modeling كامل STRIDE/Abuse Cases | Security | Architecture | P0 | Planned | T002 | S16 |
| [T081](https://app.notion.com/p/4143830ba34d815f9a3ceacaaefa3913) | نظام Identity وRBAC/ABAC متدرج | Security | Core | P0 | Planned | T047,T080 | S16 |
| [T082](https://app.notion.com/p/4143830ba34d8100b829f51b6c057158) | تقوية Sandbox ضد مشاريع عدائية | Security | Production | P0 | Planned | T065,T080 | S13 |
| [T083](https://app.notion.com/p/4143830ba34d81b3bf88ed8bdd627b7c) | تصميم Secret/PII Redaction شامل | Security | Production | P0 | Planned | T033,T080 | S16 |
| [T084](https://app.notion.com/p/4143830ba34d8136b0dcf26a7b1742e8) | مقاومة Prompt Injection في Repository/Telemetry | Security | Production | P0 | Planned | T050,T080 | S16 |
| [T085](https://app.notion.com/p/4143830ba34d81ec97d6ef8cd0e2243e) | Supply Chain Hardening وSBOM وتوقيع Artifacts | Security | Production | P0 | Planned | T005,T080 | S16 |
| [T086](https://app.notion.com/p/4143830ba34d8112a99ae6d3dfddfd1f) | اختبار اختراق داخلي ومراجعة أمن مستقلة | Security | Production | P0 | Planned | T081,T082,T083,T084 | S16 |
| [T087](https://app.notion.com/p/4143830ba34d8148be47f666dfcaa243) | تأمين Web/API ضد SSRF/CSRF/Path Traversal | Security | Production | P0 | Planned | T047,T080 | S16 |
| [T088](https://app.notion.com/p/4143830ba34d81d1a20ad86db7a1b1dd) | سياسات Privacy Export/Delete/Retention | Security | Production | P0 | Planned | T029,T032 | S16 |
| [T089](https://app.notion.com/p/4143830ba34d81cf99aff3628656f849) | بنية Unit/Integration/Contract Tests العامة | Quality | Core | P0 | Planned | T005,T006 | S17 |
| [T090](https://app.notion.com/p/4143830ba34d81a68addf6af397e435f) | Property-based وModel Checking لانتقالات الحالة | Quality | Production | P0 | Planned | T055,T071,T089 | S17 |
| [T091](https://app.notion.com/p/4143830ba34d8156af6cc97ac043c1c5) | Benchmarks كبيرة وضغط مستمر | Quality | Production | P0 | Planned | T017,T036,T043 | S17 |
| [T092](https://app.notion.com/p/4143830ba34d81b1892dd05cb5c89ad8) | اختبار E2E شامل لكل وحدات ArchPulse | Quality | Production | P0 | Planned | T025,T030,T067,T075,T061 | S17 |
| [T093](https://app.notion.com/p/4143830ba34d81138c7cd40a472ad4b9) | اختبار UX accessibility وتقييم الهندسة البصرية | Quality | Production | P0 | Planned | T043,T046 | S17 |
| [T094](https://app.notion.com/p/4143830ba34d81d5a2ccec058ed9a8a5) | دراسة مستخدمين تقارن ArchPulse بتجميع منافسين | Quality | Production | P1 | Planned | T078,T092 | S21 |
| [T095](https://app.notion.com/p/4143830ba34d81789218d179bb791c7b) | اختبار توافق OS/SDK/Database/Agents | Quality | Production | P0 | Planned | T092,T099 | S17 |
| [T096](https://app.notion.com/p/4143830ba34d817c8dbde37ec9a390df) | Corpus عام لإعادة الإنتاج والتجارب المرجعية | Quality | Ecosystem | P1 | Planned | T089,T092 | S17 |
| [T097](https://app.notion.com/p/4143830ba34d816fbf84f65f006532d0) | حزم تثبيت Local-First وتشغيل Web Studio | Operations | Integrated | P0 | Planned | T047,T008 | S18 |
| [T098](https://app.notion.com/p/4143830ba34d81b794f6e071e7fe7f9f) | نشر VPS آمن واتصال HTTPS بهوية مستخدم | Operations | Production | P0 | Planned | T097,T081 | S18 |
| [T099](https://app.notion.com/p/4143830ba34d818ea160ccd7cdd4396e) | تفعيل PostgreSQL وMulti-host Coordinator | Operations | Production | P0 | Planned | T008,T055 | S18 |
| [T100](https://app.notion.com/p/4143830ba34d81ea8c40de926a2d395d) | تعافي كوارث وتشغيل Backup Restore آلي | Operations | Production | P0 | Planned | T032,T099 | S18 |
| [T101](https://app.notion.com/p/4143830ba34d81a28b77e4aac4ab37e7) | Release Pipeline متعددة المنصات وموقعة | Operations | Production | P0 | Planned | T085,T095 | S18 |
| [T102](https://app.notion.com/p/4143830ba34d81859ddbdade0fac59e7) | Self-observability للمنصة نفسها | Operations | Production | P1 | Planned | T033,T097 | S18 |
| [T103](https://app.notion.com/p/4143830ba34d81cd813ed4698072155b) | تكامل GitHub Actions وPR Engineering Reports | Operations | Ecosystem | P1 | Planned | T020,T045,T101 | S23 |
| [T104](https://app.notion.com/p/4143830ba34d8122a500e094dbe3e684) | قرار الرخصة وترخيص التبعيات والتسمية | Open Source | Architecture | P0 | Planned | T004,T080 | S19 |
| [T105](https://app.notion.com/p/4143830ba34d81e79f30ffce8618eb6d) | إعداد وثائق Governance وSecurity وContributor | Open Source | Ecosystem | P0 | Planned | T104 | S19 |
| [T106](https://app.notion.com/p/4143830ba34d818fad1ee49122123638) | Developer Bootstrap مجاني وتعليم المساهمين | Open Source | Ecosystem | P0 | Planned | T105,T096 | S19 |
| [T107](https://app.notion.com/p/4143830ba34d815381e2c4632af48040) | أمثلة Plugin SDK واختبارات conformance للمجتمع | Open Source | Ecosystem | P1 | Planned | T007,T106 | S24 |
| [T108](https://app.notion.com/p/4143830ba34d8122a2b1d04ea8d21acc) | توثيق End User وAPI وعمليات النشر بالعربية والإنجليزية | Open Source | Ecosystem | P1 | Planned | T105,T044,T098 | S19 |
| [T109](https://app.notion.com/p/4143830ba34d8183a166caa2cd4b3e52) | تنظيم Issues/Discussions وGood First Issues | Open Source | Ecosystem | P1 | Planned | T105 | S19 |
| [T110](https://app.notion.com/p/4143830ba34d81939e71e927548b481f) | سياسة الإصدارات وحوكمة التوافق والتبعيات | Open Source | Ecosystem | P1 | Planned | T104,T105 | S19 |
| [T111](https://app.notion.com/p/4143830ba34d813e8a0ce582651ea690) | بوابة إطلاق Production كاملة لكل المكونات | Quality | Production | P0 | Planned | T092,T086,T100,T101,T104 | S20 |
| [T112](https://app.notion.com/p/4143830ba34d8125a11ded45a6501754) | تحرير المعمارية بصريًا وتحويلها لعقود قواعد | Studio | Integrated | P0 | Planned | T038,T018 | S09 |
| [T113](https://app.notion.com/p/4143830ba34d81e0ac28f89cdae7fb7d) | تحويل قرار مهندس مرئي إلى مهمة Agent ضمن موافقة | Agents | Integrated | P0 | Planned | T112,T049,T051 | S10 |
| [T114](https://app.notion.com/p/4143830ba34d8137b2aed4eddd422057) | استوديو مراجعة موحد للوكيل والتجارب والمخالفات | Studio | Production | P1 | Planned | T075,T062 | S09 |
| [T115](https://app.notion.com/p/4143830ba34d81639d13ea79c9b2c7d7) | إنشاء نسخة Graph واحدة متوافقة عبر كل الوحدات | Foundation | Production | P1 | Planned | T023,T034,T071 | S01 |
| [T116](https://app.notion.com/p/4143830ba34d81329b4df87ccdfff37f) | مراجعة نهائية للتميّز الحقيقي والاستمرارية بين الوكلاء | Quality | Production | P0 | Planned | T111,T061,T078 | S20 |
| [T117](https://app.notion.com/p/4143830ba34d81f082f8c5206259e324) | تأسيس [AGENTS.md](http://AGENTS.md) وHandoff Journal داخل مستودع ArchPulse | Open Source | Ecosystem | P0 | Planned | T005,T053,T105 | S25 |
| [T118](https://app.notion.com/p/4143830ba34d810982d0dfd87f988300) | اختبار عملي لاستئناف تطوير ArchPulse بواسطة وكيل مختلف | Continuity | Production | P0 | Planned | T117,T061 | S25 |
| [T119](https://app.notion.com/p/4143830ba34d81b1b59dda5ddac40013) | Canonical hierarchy & scope topology | Graph | Core | P0 | Planned | T009,T011 | S27 |
| [T120](https://app.notion.com/p/4143830ba34d81cfb316ca8d15c7cfe5) | Cross-scope membership & portal resolver | Graph | Core | P0 | Planned | T119,T012 | S27 |
| [T121](https://app.notion.com/p/4143830ba34d81589bedfc9b64b37aa3) | ViewScope descriptor & bounded graph query APIs | Graph | Core | P0 | Planned | T119,T011,T047 | S27 |
| [T122](https://app.notion.com/p/4143830ba34d810b9276f1f3d9f561dd) | Aggregated-edge planner with evidence counts | Graph | Integrated | P0 | Planned | T120,T121,T019 | S27 |
| [T123](https://app.notion.com/p/4143830ba34d8152a526fc8af3264adc) | Master Map & recursive scoped diagram canvas | Studio | Integrated | P0 | Planned | T038,T119,T121 | S27 |
| [T124](https://app.notion.com/p/4143830ba34d81348c51e63d85b2dfe5) | Breadcrumb, Up/Home and navigation history machine | Studio | Integrated | P0 | Planned | T123 | S27 |
| [T125](https://app.notion.com/p/4143830ba34d81c7be16f4b3cb0f2203) | Universal Search & Jump-to-symbol/feature | Studio | Integrated | P0 | Planned | T121,T123 | S27 |
| [T126](https://app.notion.com/p/4143830ba34d815296b8fe73a48be0a0) | Multi-lens switcher & flow selector within scope | Studio | Integrated | P0 | Planned | T123,T127 | S27 |
| [T127](https://app.notion.com/p/4143830ba34d819e9a86d7bd3148cc7d) | Evidence-backed FlowLensQueryEngine | Graph | Integrated | P0 | Planned | T120,T121 | S27 |
| [T128](https://app.notion.com/p/4143830ba34d8148bd5ff770cd9717a8) | Feature-scoped and module-scoped business process diagrams | Features | Integrated | P0 | Planned | T025,T120,T127 | S27 |
| [T129](https://app.notion.com/p/4143830ba34d8136bef1c3b06367d5ee) | Method-level call, CFG and async/event flow diagrams | Graph | Integrated | P0 | Planned | T014,T127 | S27 |
| [T130](https://app.notion.com/p/4143830ba34d81cbb8e8e3c88c3d5ecf) | Scoped live runtime overlays and build-revision alignment | Runtime | Integrated | P0 | Planned | T034,T042,T123 | S27 |
| [T131](https://app.notion.com/p/4143830ba34d818f8eb7c31965c5ddba) | Revision-aware architecture future navigation/diff | Futures | Integrated | P0 | Planned | T075,T123,T127 | S27 |
| [T132](https://app.notion.com/p/4143830ba34d815fa5a9f03594ec2f12) | Stable layout, semantic zoom, clustering & virtualization | Studio | Production | P0 | Planned | T123,T121 | S27 |
| [T133](https://app.notion.com/p/4143830ba34d8173a4fee28a81940aa6) | Incremental Graph UI patches and reconnect recovery | Studio | Production | P0 | Planned | T039,T123,T132 | S27 |
| [T134](https://app.notion.com/p/4143830ba34d818c9153c132f1ff3d2d) | Collapsed violation indicators & expandable evidence paths | Guardian | Integrated | P0 | Planned | T122,T040 | S27 |
| [T135](https://app.notion.com/p/4143830ba34d81f6bfb5ecfd84713d6d) | Scoped authorization for portals, search, aggregation and deep links | Security | Production | P0 | Planned | T081,T120,T125 | S27 |
| [T136](https://app.notion.com/p/4143830ba34d81ccb0e5e4bd7f46b1d6) | Bookmarks, deep links and shared saved diagram views | Studio | Production | P1 | Planned | T124,T135 | S27 |
| [T137](https://app.notion.com/p/4143830ba34d816aaf3cf3f54a25b718) | Accessible navigation: alternate tree, table and mobile focus mode | Studio | Production | P0 | Planned | T124,T126,T043 | S27 |
| [T138](https://app.notion.com/p/4143830ba34d81ad942edc2e0810635a) | Hierarchical diagram acceptance suite AV-01..AV-14 | Quality | Production | P0 | Planned | T123,T128,T129,T130,T132,T134,T135,T137 | S27 |
| [T139](https://app.notion.com/p/4143830ba34d81ceb552d3cfa88b114f) | Agent handoff preserving live diagram scope and evidence | Continuity | Production | P0 | Planned | T061,T133,T131 | S27 |
| [T140](https://app.notion.com/p/4143830ba34d8160926dc1aba06eff90) | Mega-repository navigation benchmarking and UX evaluation | Quality | Production | P0 | Planned | T017,T132,T138 | S27 |
| [T141](https://app.notion.com/p/4143830ba34d8155be14ef5112082a97) | Approved architecture rule catalog and human policy lifecycle | Guardian | Integrated | P0 | Planned | T018,T112 | S28 |
| [T142](https://app.notion.com/p/4143830ba34d81169067e3715ce97659) | Preflight architecture proposals and post-edit validation | Guardian | Integrated | P0 | Planned | T141,T019,T049 | S28 |
| [T143](https://app.notion.com/p/4143830ba34d81b19848cc5a8d003ab6) | Diagram-wide violation overlays with severity and expected paths | Studio | Integrated | P0 | Planned | T122,T123,T019 | S28 |
| [T144](https://app.notion.com/p/4143830ba34d819da8a7d4dcf4aa2833) | Explainable Violation Inspector and causal witness viewer | Studio | Integrated | P0 | Planned | T143,T012 | S28 |
| [T145](https://app.notion.com/p/4143830ba34d81319255cdca4c639784) | Architectural enforcement modes, waivers and CI policy gates | Guardian | Production | P0 | Planned | T020,T141,T021 | S28 |
| [T146](https://app.notion.com/p/4143830ba34d81fb8d93f3715310c098) | Annotation evidence ingestion from XML docs, README, ADR, tests, OpenAPI | Graph | Integrated | P0 | Planned | T014,T012,T024 | S28 |
| [T147](https://app.notion.com/p/4143830ba34d81bc88c0ddee77c6ba8d) | Documentation drift detection and annotation conflict resolver | Graph | Production | P0 | Planned | T146,T015 | S28 |
| [T148](https://app.notion.com/p/4143830ba34d81e6b520e0e9caa33823) | Provenance-rich contextual explanations in graph labels and inspectors | Studio | Integrated | P0 | Planned | T146,T144 | S28 |
| [T149](https://app.notion.com/p/4143830ba34d8111a0c1f58c15fb1e35) | Policy guardrails for agent execution vs mere prompt instructions | Agents | Production | P0 | Planned | T142,T145,T051 | S28 |
| [T150](https://app.notion.com/p/4143830ba34d815baac1c7313a85c625) | VG-01..VG-10 integration and adversarial acceptance suite | Quality | Production | P0 | Planned | T143,T145,T147,T148,T149 | S28 |
| [T151](https://app.notion.com/p/4143830ba34d81719683da26447b82d5) | Architecture-impacting decision trigger & approval-threshold policy | Agents | Integrated | P0 | Planned | T141,T049,T070 | S29 |
| [T152](https://app.notion.com/p/4143830ba34d8192b8b3f023cbe77454) | Architecture Decision Package with evidenced alternative diagrams | Futures | Integrated | P0 | Planned | T151,T075,T127 | S29 |
| [T153](https://app.notion.com/p/4143830ba34d8147a843e5442676ecab) | MCP Apps UI resource server and host-capability negotiation | Agents | Integrated | P0 | Planned | T048,T047,T152 | S29 |
| [T154](https://app.notion.com/p/4143830ba34d8106a4afda4dd48cb39d) | Chat-embedded interactive architecture alternatives and flow lenses | Studio | Integrated | P0 | Planned | T153,T126,T152 | S29 |
| [T155](https://app.notion.com/p/4143830ba34d81a8bb61dfe3b4dd8bb6) | Portable plain-MCP engineering decision fallback | Agents | Production | P0 | Planned | T153,T154 | S29 |
| [T156](https://app.notion.com/p/4143830ba34d81cfae9bdb3e86d39b68) | Durable DecisionRecord and server-validated approval receipts | Agents | Production | P0 | Planned | T151,T154,T081 | S29 |
| [T157](https://app.notion.com/p/4143830ba34d81949011d11ea6c85d04) | Execute chosen architecture after human approval and continuous revalidation | Agents | Production | P0 | Planned | T156,T058,T113 | S29 |
| [T158](https://app.notion.com/p/4143830ba34d812db7bee899193e73d1) | Cross-agent resume of awaiting architecture decision | Continuity | Production | P0 | Planned | T157,T061 | S29 |
| [T159](https://app.notion.com/p/4143830ba34d817aad39db9a63439e27) | DEC-01..DEC-12 MCP UI, decision and approval security tests | Quality | Production | P0 | Planned | T153,T155,T156,T158 | S29 |
| [T160](https://app.notion.com/p/4143830ba34d816bac54cf852caafa52) | Full architecture governance and chat decision end-to-end ERP scenario | Quality | Production | P0 | Planned | T150,T159,T092 | S29 |
| [T161](https://app.notion.com/p/4143830ba34d816f87cbd85ea3571b6d) | Stable SelectionRef for nodes, edges, scopes and source spans | Graph | Core | P0 | Planned | T009,T121,T135 | S30 |
| [T162](https://app.notion.com/p/4143830ba34d81d7aad9d5f97bda099b) | Canvas multi-selection and context action registry | Studio | Integrated | P0 | Planned | T123,T124,T161 | S30 |
| [T163](https://app.notion.com/p/4143830ba34d816fa689e644bcc6f24d) | Selection-to-Chat context composer bridge | Agents | Integrated | P0 | Planned | T162,T048,T153 | S30 |
| [T164](https://app.notion.com/p/4143830ba34d81c6bbb5c3cecf1d1b1d) | AI-response-to-canvas focus and back navigation | Studio | Integrated | P0 | Planned | T163,T125 | S30 |
| [T165](https://app.notion.com/p/4143830ba34d8195804fc626f730716e) | Create scoped agent tasks from selected graph elements | Agents | Integrated | P0 | Planned | T163,T113,T156 | S30 |
| [T166](https://app.notion.com/p/4143830ba34d81859282d9565b7871d1) | Graph selection into interactive engineering decisions | Agents | Integrated | P0 | Planned | T165,T152,T154 | S30 |
| [T167](https://app.notion.com/p/4143830ba34d8187bae6d30314294732) | Selection reference authorization, expiry and collision security | Security | Production | P0 | Planned | T161,T081,T135 | S30 |
| [T168](https://app.notion.com/p/4143830ba34d816893aadd6af367426d) | Cross-agent preservation of exact selected symbols and tasks | Continuity | Production | P0 | Planned | T158,T161 | S30 |
| [T169](https://app.notion.com/p/4143830ba34d81b684f5e6b10675c065) | Canvas↔AI bidirectional acceptance suite BC-01..10 | Quality | Production | P0 | Planned | T162,T163,T164,T165,T166,T167,T168 | S30 |
| [T170](https://app.notion.com/p/4143830ba34d81ef8544eba6a60877f1) | Versioned method–test association contract and provenance | Graph | Core | P0 | Planned | T009,T014,T064 | S31 |
| [T171](https://app.notion.com/p/4143830ba34d8150a011d3e75c14db4d) | Precise xUnit/NUnit/MSTest discovery and filter adapters | RepoLab | Integrated | P0 | Planned | T064,T170 | S31 |
| [T172](https://app.notion.com/p/4143830ba34d8123b598dbc2671f8060) | Test association inference via Roslyn and optional coverage evidence | Graph | Integrated | P0 | Planned | T170,T171,T014 | S31 |
| [T173](https://app.notion.com/p/4143830ba34d81bf8361fc2e9a7f0cc5) | Node-inline test badges and Inspector Tests tab (no test blocks) | Studio | Integrated | P0 | Planned | T170,T123,T143 | S31 |
| [T174](https://app.notion.com/p/4143830ba34d815e979ad5a0241ca1f3) | Run linked tests for selected method or feature with sandbox | RepoLab | Integrated | P0 | Planned | T171,T065,T066,T162 | S31 |
| [T175](https://app.notion.com/p/4143830ba34d81fba22cee20b69e2fd9) | Inline test run state, freshness and revision-bound receipts | Studio | Integrated | P0 | Planned | T173,T174,T039 | S31 |
| [T176](https://app.notion.com/p/4143830ba34d8194859cd4f57dd51177) | GetLinkedTests and RunLinkedTests MCP from selected graph nodes | Agents | Integrated | P0 | Planned | T163,T174,T048 | S31 |
| [T177](https://app.notion.com/p/4143830ba34d811095b1f3a5059851fa) | Aggregated test indicators on service/module/feature maps | Studio | Integrated | P0 | Planned | T173,T122,T025 | S31 |
| [T178](https://app.notion.com/p/4143830ba34d8175b5edc979243a8e61) | Recover cancelled/crashed test run with stable receipts | Continuity | Production | P0 | Planned | T174,T058,T175 | S31 |
| [T179](https://app.notion.com/p/4143830ba34d8121ab2ade9dfffb577b) | In-place test association and scoped runner acceptance IT-01..12 | Quality | Production | P0 | Planned | T171,T172,T173,T174,T175,T176,T177,T178 | S31 |
| [T180](https://app.notion.com/p/4143830ba34d81b48361efdfd63cca4c) | Mermaid generator for every architecture alternative | Futures | Integrated | P0 | Planned | T152,T127,T121 | S32 |
| [T181](https://app.notion.com/p/4143830ba34d81c9a43afff6cc0f290a) | Mermaid↔graph references mapping and scoped exports | Graph | Integrated | P0 | Planned | T180,T012,T161 | S32 |
| [T182](https://app.notion.com/p/4143830ba34d81e7afbbc273a7584ea9) | Mermaid syntax validation, safe escaping and complexity budgets | Security | Production | P0 | Planned | T180,T087,T084 | S32 |
| [T183](https://app.notion.com/p/4143830ba34d8148bf71e5e58286a897) | Mandatory per-option Mermaid fallback for every MCP host | Agents | Integrated | P0 | Planned | T180,T153,T155 | S32 |
| [T184](https://app.notion.com/p/4143830ba34d81ccb98cf6b2ef538cc4) | Mermaid preview/source/copy and conceptual status labels | Studio | Integrated | P0 | Planned | T181,T183,T126 | S32 |
| [T185](https://app.notion.com/p/4143830ba34d81e3b30dc50bd351991b) | Mermaid contract conformance MD-01..10 and decision approval invalidation | Quality | Production | P0 | Planned | T180,T181,T182,T183,T184 | S32 |
| [T186](https://app.notion.com/p/4143830ba34d817b978cc159f620a0fa) | Test Journey schema, run identity and evidence contracts | RepoLab | Core | P0 | Planned | T170,T171,T008 | S33 |
| [T187](https://app.notion.com/p/4143830ba34d8115894dc2f9b81e923b) | VSTest and Microsoft.Testing.Platform result adapter compatibility | RepoLab | Integrated | P0 | Planned | T186,T171,T066 | S33 |
| [T188](https://app.notion.com/p/4143830ba34d81009310cbe04099c730) | Per-test Activity root and trace context propagation | Runtime | Integrated | P0 | Planned | T186,T187,T033 | S33 |
| [T189](https://app.notion.com/p/4143830ba34d81f2b420e5b078c8936b) | Standard .NET OTEL instrumentation for test call journeys | Runtime | Integrated | P0 | Planned | T188,T033,T034 | S33 |
| [T190](https://app.notion.com/p/4143830ba34d8132ad2df7b4371a9475) | Opt-in deep method entry/exit/exception instrumentation adapters | Runtime | Integrated | P0 | Planned | T188,T189,T037 | S33 |
| [T191](https://app.notion.com/p/4143830ba34d81599080cad821c15c30) | Safe argument/return snapshot and redaction policy | Security | Production | P0 | Planned | T190,T083,T088 | S33 |
| [T192](https://app.notion.com/p/4143830ba34d81b3ad11f538794987b0) | Assertion and exception evidence capture providers | RepoLab | Integrated | P0 | Planned | T187,T190 | S33 |
| [T193](https://app.notion.com/p/4143830ba34d81c59415f58b66d7b40e) | Trace-to-symbol correlation with source and build revisions | Graph | Integrated | P0 | Planned | T189,T012,T014 | S33 |
| [T194](https://app.notion.com/p/4143830ba34d818ba483c1ef35edd7f3) | Async, parallel and distributed call causality resolver | Runtime | Production | P1 | Planned | T188,T189,T193 | S33 |
| [T195](https://app.notion.com/p/4143830ba34d8170acafd7b8c338e260) | Per-test versus suite-wide coverage provenance reconciliation | RepoLab | Production | P1 | Planned | T172,T187,T189 | S33 |
| [T196](https://app.notion.com/p/4143830ba34d81a6a2d8c90b0b229f9f) | Durable test journey event log and replay artifact store | Runtime | Integrated | P0 | Planned | T186,T189,T008 | S33 |
| [T197](https://app.notion.com/p/4143830ba34d81c59d80ffc4fa6f4aea) | Streaming test journey progress and cursor reconnect | Runtime | Integrated | P0 | Planned | T196,T039 | S33 |
| [T198](https://app.notion.com/p/4143830ba34d8146aa1fd46e45db2abf) | Observed test path overlay on existing architecture diagram nodes | Studio | Integrated | P0 | Planned | T193,T196,T123,T173 | S33 |
| [T199](https://app.notion.com/p/4143830ba34d81e48d44efd38ca4bf1f) | Standalone on-demand Test Flow and Mermaid export lenses | Studio | Integrated | P0 | Planned | T196,T127,T181 | S33 |
| [T200](https://app.notion.com/p/4143830ba34d81e69998e02d5cd64cc4) | Invocation input/output, exception and assertion inspector with step controls | Studio | Integrated | P0 | Planned | T191,T192,T198,T199 | S33 |
| [T201](https://app.notion.com/p/4143830ba34d8173ac67f00cac69d2e6) | Compare observed journeys between commits and architecture candidates | Futures | Production | P1 | Planned | T196,T199,T079 | S33 |
| [T202](https://app.notion.com/p/4143830ba34d81f082ead9e2888ed601) | MCP TestRun/Step selection and AI evidence explanation tools | Agents | Integrated | P0 | Planned | T176,T163,T196,T199 | S33 |
| [T203](https://app.notion.com/p/4143830ba34d8141a85ec998d54bf054) | Test journey sandbox protection, RBAC, payload privacy and retention | Security | Production | P0 | Planned | T191,T196,T202,T082 | S33 |
| [T204](https://app.notion.com/p/4143830ba34d8100a1bff0ff590871fa) | Crash-safe tracked test execution and agent handoff | Continuity | Production | P0 | Planned | T196,T197,T058,T178 | S33 |
| [T205](https://app.notion.com/p/4143830ba34d81239ebfc20c1d03e30f) | Test Journey end-to-end acceptance suite TJ-01..TJ-18 and instrumentation benchmarks | Quality | Production | P0 | Planned | T187,T188,T189,T190,T191,T192,T193,T194,T195,T196,T197,T198,T199,T200,T201,T202,T203,T204 | S33 |
