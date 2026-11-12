# Requirements traceability — S26 snapshot

For snapshot preservation, contributor review rules, and automated negative checks, see the [requirement baseline lock guide](baseline-lock-guide.md). The [machine-readable requirements registry](machine-readable-registry.md) exports this proposed S26 snapshot and its task/specification links deterministically for agents and tooling. Passing these checks does **not** establish implemented functionality.

> Original source: [Notion S26](https://app.notion.com/p/4143830ba34d8185a0edc83f974c55cd). All requirements below are **planned** until independent code, tests and approved evidence demonstrate completion. Imported for contributor visibility; review source for later changes.

| Requirement ID | Expected product behavior | Notion tasks | Acceptance evidence required | Implementation status |
| --- | --- | --- | --- | --- |
| R-CORE-001 | جميع القدرات المعتمدة جزء من الإصدار الإنتاجي الكامل | T001,T111 | Full capability acceptance suite | Planned |
| R-CORE-002 | النواة Local-First مستقلة عن IDE والـAI | T002,T005,T097 | Install+offline+CLI without vendor key | Planned |
| R-CORE-003 | واجهات عامة وإصدارات عقود متوافقة | T006,T047 | OpenAPI+backward contract tests | Planned |
| R-CORE-004 | نموذج Graph موحد متعدد المستويات | T009,T011,T115 | System→Method traversal fixture | Planned |
| R-CORE-005 | لكل علاقة Evidence/Revision/Source | T012,T010 | Zero orphan provenance edges | Planned |
| R-CORE-006 | Graph incremental correct after rename/changes | T014,T015 | Incremental vs full-scan equivalence | Planned |
| R-CORE-007 | دعم حلول .NET متعددة الأنواع والمشاريع | T013,T014 | [ASP.NET/EF/Generics](http://ASP.NET/EF/Generics) fixtures | Planned |
| R-CORE-008 | استعلامات Graph محدودة الموارد | T011,T017 | Huge corpus bounded query benchmark | Planned |
| R-CORE-009 | إعادة بناء Graph عقب crash | T010,T008,T032 | Replay+backup restore determinism | Planned |
| R-CORE-010 | مكونات plugins قابلة للتطوير | T007,T016,T107 | Third-party conformance | Planned |
| R-CORE-011 | Data ownership وحفظ آمن للإصدارات | T008,T032 | Migration/restore/retention tests | Planned |
| R-CORE-012 | Graph displays uncertain coverage | T012,T115 | Unknown/inferred never marked verified | Planned |
| R-GUARD-001 | قواعد Layers/Boundaries/Cycles قابلة للتنفيذ | T018,T019 | Golden architecture violations | Planned |
| R-GUARD-002 | انحراف المعمارية مرتبط بأدلة ورموز | T019,T040,T022 | Witness path source inspection | Planned |
| R-GUARD-003 | CI يميّز Pass وViolation وUnknown | T020,T103 | PR policy failure fixture | Planned |
| R-GUARD-004 | Waiver مؤقت مع تاريخ وموافقة | T021 | Expiration automatically reactivates finding | Planned |
| R-GUARD-005 | إنشاء سياسة من واجهة الرسم | T112,T113 | Visual boundary generates tested rule | Planned |
| R-FEAT-001 | ربط الوظيفة التجارية بكودها واختباراتها | T023,T024 | ERP feature-to-symbol corpus | Planned |
| R-FEAT-002 | عدم اعتماد تخمين AI دون شاهد | T024,T026 | Provenance and human confirmation | Planned |
| R-FEAT-003 | Impact analysis على مستوى الوظيفة | T025,T041 | PR impacts known business features | Planned |
| R-FEAT-004 | تصحيح الخرائط البشرية مع Versioning | T026 | Human correction survives reindex | Planned |
| R-DATA-001 | تتبع Source→Sink ساكن بحدود صريحة | T027,T028 | Labeled safe/unsafe/unknown flows | Planned |
| R-DATA-002 | دمج المسار التشغيلي مع الساكن دون خلط | T028,T030,T034 | Static possible vs runtime observed labels | Planned |
| R-DATA-003 | كشف انتهاكات تدفق حساس مع تقليل false alarms | T029,T031 | Sanitizer and taint fixtures | Planned |
| R-DATA-004 | تعتيم PII وقواعد retention | T029,T088 | No secrets in logs/prompts/Graph | Planned |
| R-LAB-001 | اكتشاف وتشغيل سيناريو/اختبار موجود | T064,T066 | xUnit/NUnit/MSTest report | Planned |
| R-LAB-002 | عزل تشغيل الشيفرة غير الموثوقة | T065,T082 | Sandbox escape denial test | Planned |
| R-LAB-003 | Trace timeline قابل للتصفح إلى المصدر | T067,T042 | Recorded path/source mapping | Planned |
| R-LAB-004 | نتائج قابلة للإعادة مع Manifest | T068 | Pinned environment repeat test | Planned |
| R-LAB-005 | استكمال الاختبارات بعد انقطاع الوكيل | T069,T077 | Resume verified partial run | Planned |
| R-LIVE-001 | OTLP Traces+Metrics+Logs من التطبيق | T033 | Ingestion with throughput controls | Planned |
| R-LIVE-002 | Correlation مع Service/Commit/Build | T034 | Reject wrong SHA attribution | Planned |
| R-LIVE-003 | خريطة خدمات حية بمؤشرات الخطأ والكمون | T035,T042 | Live service map linked trace | Planned |
| R-LIVE-004 | Sampling وضغط الحمل والخصوصية | T036,T083 | No PII; measured drop handling | Planned |
| R-LIVE-005 | Instrument selected methods without blanket tracing | T037 | Overhead and timeout measured | Planned |
| R-UI-001 | رسم من النظام إلى Method على شكل تفاعلي | T038 | Large graph drill-down usability | Planned |
| R-UI-002 | التحديث الحي مع تخطي انقطاع الشبكة | T039 | Cursor catch-up + snapshot consistency | Planned |
| R-UI-003 | سياق موحد للمخالفات والميزات والبيانات | T040,T041,T114 | Cross-perspective node navigation | Planned |
| R-UI-004 | Timeline تشغيل حي مؤرخ للإصدار | T042,T034 | No inferred runtime claim | Planned |
| R-UI-005 | رسم بصري لتغيير التصميم يصنع Rule | T112 | UI-to-contract validation | Planned |
| R-UI-006 | تكامل VS Code دون محرك تحليل مكرر | T044 | Same data local and VS Code | Planned |
| R-UI-007 | CLI آلي بعقود وأكواد خروج | T045 | Headless CI fixtures | Planned |
| R-UI-008 | متصفح الهاتف والاتصال البعيد الآمن | T046,T098 | Authenticated expiring deep links | Planned |
| R-UI-009 | Accessibility وVirtualization للـGraph الكبير | T043,T093 | Keyboard and large corpus tests | Planned |
| R-AGENT-001 | MCP read/plan/execute مع فصل الأذونات | T048,T051 | Authorization scope test | Planned |
| R-AGENT-002 | Codex Adapter يربط العمل الفعلي بالرسم | T049 | Saved file change reindexed and evidenced | Planned |
| R-AGENT-003 | دعم وكيل بديل مستقل عن مزود | T050 | Neutral adapter contract suite | Planned |
| R-AGENT-004 | قرار الرسم يتحول لمهمة بحدود اعتماد | T113 | Human approval before execution | Planned |
| R-AGENT-005 | عرض Timeline لوكلاء متعددين | T052,T062 | No unverified action shown as success | Planned |
| R-HAND-001 | TaskEnvelope محايد قابل للنقل | T053 | Cross-agent schema compatibility | Planned |
| R-HAND-002 | Checkpoint immutable وArtifact hashes | T054,T056 | Restore tamper-detected checkpoint | Planned |
| R-HAND-003 | Lease monotonic fencing ضد split-brain | T055,T060 | Stale agent write rejected | Planned |
| R-HAND-004 | التحقق من الآثار قبل retry | T058,T063 | No duplicate PR create after timeout | Planned |
| R-HAND-005 | نقل المهمة من Codex لوكيل آخر | T057,T061 | Different provider continues safely | Planned |
| R-HAND-006 | حالات Unknown/RecoveryRequired واضحة | T058,T059 | No unknown side effect promoted to pass | Planned |
| R-HAND-007 | استعادة الحالة حتى لو عاد وكيل قديم | T055,T060 | Old lease epoch ignored | Planned |
| R-HAND-008 | صلاحيات وموافقات لا تورث آليًا | T051,T059 | Re-evaluate policy post takeover | Planned |
| R-HAND-009 | استمرار تطوير ArchPulse ذاته عبر [AGENTS.md](http://AGENTS.md) | T117,T118 | Developer Agent B succeeds without chat memory | Planned |
| R-FUT-001 | Architectural Experiment Transaction formal state | T070,T071 | Valid state transitions and replay | Planned |
| R-FUT-002 | تنفيذ بدائل معزولة في Worktrees | T072,T065 | Baseline unchanged by candidates | Planned |
| R-FUT-003 | مقارنة أحمال منصفة قابلة للتكرار | T073,T074 | Equivalence environment+dataset report | Planned |
| R-FUT-004 | مشاهدة بدائل ومقاييسها على الرسم | T075 | Visual diff with evidence labels | Planned |
| R-FUT-005 | قرار إنساني قبل دمج التصميم | T076 | Approval and scope audit | Planned |
| R-FUT-006 | استئناف التجارب بغير الوكيل السابق | T077 | Crash during candidate then resumed | Planned |
| R-FUT-007 | مراقبة التصميم المعتمد أثناء التشغيل | T079 | Drift vs decision baseline | Planned |
| R-SEC-001 | Threat model شامل وواضح | T080 | Trust-boundary abuse case table | Planned |
| R-SEC-002 | RBAC/ABAC وعزل الأوامر الحساسة | T081,T087 | Cross-role denial matrix | Planned |
| R-SEC-003 | Sandbox اختبار فعلي ضد مشاريع عدائية | T082,T086 | Independent exploit-case review | Planned |
| R-SEC-004 | Prompt-injection hostile inputs treated as data | T084 | Malicious README can't override policies | Planned |
| R-SEC-005 | Supply chain SBOM/signing | T085,T101 | Verified signed release artifacts | Planned |
| R-SEC-006 | تصدير وحذف بيانات المستخدم والتليمترية | T088 | Retention/data deletion tests | Planned |
| R-QA-001 | مصفوفة اختبارات متعددة الطبقات | T089,T092 | Requirement to test proof | Planned |
| R-QA-002 | فحص Formal Invariants للـAgent/AET | T090 | Fault model + counterexample checks | Planned |
| R-QA-003 | Benchmarks مستقلة وتقييم مستخدمين | T091,T094 | Published scripts and limitations | Planned |
| R-QA-004 | اختبار تكامل كل الوظائف دون إهمال | T092,T111,T116 | Full ERP end-to-end acceptance | Planned |
| R-OPS-001 | تثبيت ذاتي محلي وخادم VPS | T097,T098 | Cross-platform protected installation | Planned |
| R-OPS-002 | Multi-host coordination and disaster recovery | T099,T100 | Restore and concurrent lease tests | Planned |
| R-OPS-003 | CI/release signed artifacts and GitHub PR reports | T101,T103 | Reproducible release and policy check | Planned |
| R-OSS-001 | رخصة مفتوحة المصدر وحوكمة شفافة | T104,T105 | Approved license and governance | Planned |
| R-OSS-002 | مساهم جديد يبني المشروع دون اشتراك AI | T106,T107 | Fresh-user free build test | Planned |
| R-OSS-003 | Documentation, support, semver and contribution | T108,T109,T110 | Contributor and compatibility reviews | Planned |
| R-NAV-001 | Master system map قابل للتوسع إلى المخطط المنفصل للوحدة والخدمة والكلاس والدالة | T119,T121,T123 | AV-01 System→Method مع Breadcrumb صحيح | Planned |
| R-NAV-002 | Canonical ownership hierarchy دون حلقات وبدون خلط العلاقة المكانية بالاستدعاء | T119,T123 | AV-01 مع rename وnon-tree code relations | Planned |
| R-NAV-003 | Feature multi-membership عبر مجالات دون تعدد ملكية زائف | T120,T128 | AV-02 CreateInvoice cross-module | Planned |
| R-NAV-004 | Cross-Scope Portals للعلاقات بين الوحدات وفتح وجهة التواصل | T120,T122 | AV-02 مع شاهد الاتصال والانتقال | Planned |
| R-NAV-005 | خيار View Flow متاح لأي Module/Service/Feature/Endpoint/Method مدعوم | T126,T127 | AV-03 اختيار المصدر والوجهة والنوع | Planned |
| R-NAV-006 | عدة عدسات بنيوية ودوال وتدفق وبيانات وتشغيل واختبارات وتجارب | T126,T127 | AV-03 مع unavailable/unknown بدل رسم مختلق | Planned |
| R-NAV-007 | Feature Flow على شكل رحلة أعمال من API إلى البيانات والاختبارات | T128 | AV-02 cross-boundary business flow | Planned |
| R-NAV-008 | Service/Module Flow على مستوى الخدمات الداخلية والخارجية | T128,T129 | AV-02/AV-04 مع Ports/Handlers | Planned |
| R-NAV-009 | Method CFG/Call/Async diagrams بدليل Semantic Roslyn وحدود تحليل واضحة | T129 | AV-04 overloaded/async/reflection | Planned |
| R-NAV-010 | Breadcrumb وHome/Up/Back/Forward مع إعادة حالة التنقل | T124,T136 | AV-01/AV-09 browser history persistence | Planned |
| R-NAV-011 | بحث عالمي Jump-to-Symbol/Service/Feature/Endpoint مع التفريق بين المتشابهات | T125 | AV-09 search/disambiguation/deep navigate | Planned |
| R-NAV-012 | Layout مستقر وSemantic Zoom ومواقع محفوظة أثناء التحديث | T132 | AV-07 layout stability after code change | Planned |
| R-NAV-013 | Lazy query + virtualized canvas + node budget مع مشاريع ضخمة | T121,T132,T140 | AV-05 100k symbols/1m edges repeatable load | Planned |
| R-NAV-014 | تجميع الحواف والقواعد مع عدم إخفاء Warning/Violation | T122,T134 | AV-06 flag collapsed P0 violation | Planned |
| R-NAV-015 | Live Graph deltas مرتبطة بالإصدار، مع استعادة فقد الرسائل وانتقال الوكلاء | T133,T139 | AV-07/AV-11 no ghost/double applied nodes | Planned |
| R-NAV-016 | Runtime Overlay مع Build SHA وSampling/Environment/TimeRange | T130 | AV-08 mismatched build warning | Planned |
| R-NAV-017 | صلاحيات عقد وروابط محمية وبحث دون تسريب مواقع/عدادات حساسة | T135,T136 | AV-10 unauthorized search/deep link/portal | Planned |
| R-NAV-018 | بدائل Accessible Tree/Table وKeyboard وMobile focus | T137 | AV-13 keyboard-only with screen-reader | Planned |
| R-NAV-019 | Revision & AET candidate diagram diff وتاريخ تغير النظام | T131,T139 | AV-12 baseline/variant lens same scope | Planned |
| R-NAV-020 | منظومة AV-01..AV-14 مع Benchmarks وقياس سهولة التنقل | T138,T140 | AV-01..AV-14 + documented user study AV-14 | Planned |
| R-VGOV-001 | تعريف قواعد معمارية قابلة للتنفيذ من التصميم المعتمد لا من افتراضات AI | T141 | VG-01 | Planned |
| R-VGOV-002 | Versioned lifecycle Draft→Active مع صاحب صلاحية ومراجعة | T141,T145 | VG-01,VG-07 | Planned |
| R-VGOV-003 | فحص مبدئي لتغيير الوكيل قبل تنفيذ الكود مع حالة PredictedRisk | T142 | VG-06 | Planned |
| R-VGOV-004 | فحص Roslyn بعد حفظ التغيير وإظهار شاهد مخالفته الحقيقي | T142,T144 | VG-02,VG-04 | Planned |
| R-VGOV-005 | ظهور تحذيرات في Master/Module/Service/Class/Method وcollapsed counts | T143 | VG-03 | Planned |
| R-VGOV-006 | Inspector يشرح القاعدة والمسار الفعلي والمتوقع ومكان الخطأ | T144 | VG-02 | Planned |
| R-VGOV-007 | Mandatory/Advisory enforcement في CI وحماية الفرع عند تفعيلها | T145 | VG-05 | Planned |
| R-VGOV-008 | Waivers بإذن ومدة وانتهاء ومراجعة وتدقيق | T145 | VG-07 | Planned |
| R-VGOV-009 | جمع XML docs وREADME وADR وOpenAPI وTests وRoslyn كAnnotations | T146 | VG-08 | Planned |
| R-VGOV-010 | كل تعليق يحمل SourceURI/Revision/AuthorOrAnalyzer/Evidence | T146,T148 | VG-08 | Planned |
| R-VGOV-011 | اكتشاف تضارب التوثيق القديم مع الكود الحالي Documentation Drift | T147 | VG-09 | Planned |
| R-VGOV-012 | شرح AI والترجمة يظهران موسومين Inferred بلا حقائق مختلقة | T148 | VG-09 | Planned |
| R-VGOV-013 | منع Prompt Injection من التعليقات أو الأدلة لتغيير Policy | T149 | VG-10 | Planned |
| R-VGOV-014 | أذونات التنفيذ/المراجعة والتغيير محكومة بالخادم لا بتعليمات Prompt فقط | T149,T145 | VG-05,VG-10 | Planned |
| R-VGOV-015 | اختبارات End-to-End شاملة لتحذير بصري وتعليق موثق وإصلاح | T150,T160 | VG-01..VG-10 | Planned |
| R-MCP-DEC-001 | قرار معماري مؤثر يكتشفه الوكيل ويوقف العملية المقيّدة | T151 | DEC-01 | Planned |
| R-MCP-DEC-002 | لا يُرهَق المهندس بطلب موافقة لكل تعديل محلي بسيط | T151 | DEC-02 | Planned |
| R-MCP-DEC-003 | Architecture Decision Package مرقم ويحمل النطاق والقيود والإصدار | T152 | DEC-03 | Planned |
| R-MCP-DEC-004 | خيارات واقعية متعددة مع معايير إيجابية/سلبية ومجهولات | T152 | DEC-03 | Planned |
| R-MCP-DEC-005 | لكل بديل رسم معماري وFlow Lens ومراجع وأثر على النظام | T152,T154 | DEC-03,DEC-04 | Planned |
| R-MCP-DEC-006 | فصل توقعات الأداء والتكلفة عن Benchmarks المقاسة فعليًا | T152 | DEC-03 | Planned |
| R-MCP-DEC-007 | دعم MCP Apps ui:// لعرض Widget تفاعلي داخل ChatGPT المتوافق | T153,T154 | DEC-04 | Planned |
| R-MCP-DEC-008 | Capability Negotiation مع مضيف MCP قبل اختيار شكل UI | T153 | DEC-05,DEC-11 | Planned |
| R-MCP-DEC-009 | Fallback منظم نصي + diagram source/link وWeb Studio للمضيف غير المتوافق | T155 | DEC-05 | Planned |
| R-MCP-DEC-010 | اختيار البديل وتعديل القيود وطلب مقارنة وتجربة من Widget | T154,T156 | DEC-08 | Planned |
| R-MCP-DEC-011 | ApprovalReceipt موثق من الخادم مع actor/Scope/Revision/TTL | T156 | DEC-06 | Planned |
| R-MCP-DEC-012 | رفض الموافقات المنتهية/المكررة/المخالفة للسياسات | T156 | DEC-07 | Planned |
| R-MCP-DEC-013 | وكيل يبدأ التنفيذ فقط بعد التحقق من القرار ثم يعيد فحص الكود | T157 | DEC-08,DEC-09 | Planned |
| R-MCP-DEC-014 | Handoff: وكيل جديد يواصل بعد انتظار قرار الإنسان دون تاريخ المحادثة | T158 | DEC-10 | Planned |
| R-MCP-DEC-015 | ثبات السلوك والأمان بين ChatGPT ومضيف MCP نصي والويب | T155,T159 | DEC-11 | Planned |
| R-MCP-DEC-016 | تغير القيود يبطِل الخيارات/الموافقات القديمة عند التعارض مع سجل مراجعة | T156,T159,T160 | DEC-12 | Planned |
| R-BCANVAS-001 | اختيار Node أو Edge أو Subgraph من المخطط | T161,T162 | BC-01 | Planned |
| R-BCANVAS-002 | هوية SymbolId دقيقة ومتوافقة مع SourceSpan/Overload | T161,T167 | BC-02 | Planned |
| R-BCANVAS-003 | Ask AI من العنصر مع مرجع SelectionRef في المحادثة | T163 | BC-03 | Planned |
| R-BCANVAS-004 | إجابة AI تعيد التركيز إلى Node/Edge أو fallback link | T164 | BC-04 | Planned |
| R-BCANVAS-005 | التحويل من الرسم إلى اقتراح مهمة لا تنفيذ تلقائي | T165 | BC-05 | Planned |
| R-BCANVAS-006 | اختيار عنصر ينشئ ADP مرتبطًا به عند قرار معماري | T166 | BC-06 | Planned |
| R-BCANVAS-007 | رفض SelectionRef منتهي/قديم مع حل تعارض الإصدار | T167 | BC-07 | Planned |
| R-BCANVAS-008 | صلاحيات Search/Chat/GraphRef تمنع تسرب عناصر حساسة | T167 | BC-08 | Planned |
| R-BCANVAS-009 | Keyboard/Mobile ومجموعة اختيار متعددة متاحة | T162,T169 | BC-09 | Planned |
| R-BCANVAS-010 | نقل SelectedRef إلى وكيل جديد بعد الانقطاع | T168 | BC-10 | Planned |
| R-BCANVAS-011 | قائمة Context Actions تشمل شرح وImpact وPlan وTests | T162,T163 | BC-03,BC-05 | Planned |
| R-BCANVAS-012 | ربط مسار محادثة AI بالمكون والمصدر لا الاسم النصي | T163,T164 | BC-02,BC-04 | Planned |
| R-BCANVAS-013 | الوكيل لا يغير نطاق المهمة دون مراجعة واضحة | T165,T167 | BC-05,BC-08 | Planned |
| R-BCANVAS-014 | اختبار قبول شامل للتفاعل ثنائي الاتجاه | T169 | BC-01..BC-10 | Planned |
| R-ITEST-001 | إظهار الاختبارات على الدالة ذاتها دون صناديق مستقلة | T170,T173 | IT-01 | Planned |
| R-ITEST-002 | اكتشاف xUnit/NUnit/MSTest بدقة مع test filters | T171 | IT-02 | Planned |
| R-ITEST-003 | تمييز الربط المباشر وغير المباشر والمرصود والمستنتج | T170,T172 | IT-03 | Planned |
| R-ITEST-004 | دالة بلا اختبار تظهر No Tests Mapped ولا ينشأ اختبار تلقائي | T173 | IT-04 | Planned |
| R-ITEST-005 | نتيجة اختبار قديم تتحول إلى Stale بعد تغير الكود | T175 | IT-05 | Planned |
| R-ITEST-006 | Run Selected Existing Tests من الدالة داخل Sandbox | T174 | IT-06 | Planned |
| R-ITEST-007 | تجميع الحالات Parameterized بدون عد مزدوج | T171,T177 | IT-07 | Planned |
| R-ITEST-008 | منع تشغيل اختبارات على الإنتاج أو دون تصريح | T174,T179 | IT-08 | Planned |
| R-ITEST-009 | استعادة التنفيذ الجزئي من RunReceipt عبر وكيل جديد | T178 | IT-09 | Planned |
| R-ITEST-010 | حالة الاختبارات على الخدمة/الميزة ملخصة دون test nodes | T177 | IT-10 | Planned |
| R-ITEST-011 | اختبارات الدالة المختارة متاحة نفسها في MCP وWeb | T176 | IT-11 | Planned |
| R-ITEST-012 | Mocked/Behavioral tests لا تدعي observed coverage | T172 | IT-12 | Planned |
| R-ITEST-013 | TestCase موجودة في backend فقط وتظل مخفية في Canvas الأساسي | T170,T173 | IT-01,IT-10 | Planned |
| R-ITEST-014 | اختبار End-to-End لكامل الربط والتشغيل والحالات | T179 | IT-01..IT-12 | Planned |
| R-MERMAID-001 | كل بديل هندسي له Mermaid Source حتى لو لم يبنَ | T180,T183 | MD-01 | Planned |
| R-MERMAID-002 | وصف Proposed أو Unknown دون اختراع تنفيذ أو قياس | T180 | MD-02 | Planned |
| R-MERMAID-003 | دعم أنواع flowchart/sequence/class/state/ER مع فحص syntax | T180,T182 | MD-03 | Planned |
| R-MERMAID-004 | إظهار Mermaid حرفيًا في مضيف MCP النصي مع رابط ويب | T183 | MD-04 | Planned |
| R-MERMAID-005 | MCP Apps يعرض البديل التفاعلي ويتيح مصدر Mermaid للنسخ | T183,T184 | MD-05 | Planned |
| R-MERMAID-006 | تعقيم Mermaid من HTML/JS/Links ضارة ومداخل عدائية | T182 | MD-06 | Planned |
| R-MERMAID-007 | التمييز بين الرسم المستنتج والمرصود والمثبت | T184 | MD-07 | Planned |
| R-MERMAID-008 | تقليص مخطط المشروع العملاق إلى Scope summary مستوفى | T180,T182 | MD-08 | Planned |
| R-MERMAID-009 | ربط IDs عناصر Mermaid بـGraphRef الحقيقية حيث تتوفر | T181 | MD-09 | Planned |
| R-MERMAID-010 | تغيير القيود يجدد Mermaid ويبطل اعتمادًا قديمًا عند اللزوم | T185 | MD-10 | Planned |
| R-TJ-001 | اختيار اختبار موجود على Method/Feature دون إنشاء TestCase مرئي منفصل | T186,T187,T198 | TJ-01,TJ-07 | Planned |
| R-TJ-002 | بدء Run & Trace باستخدام Runner/Sandbox وصلاحية صريحة | T187,T203 | TJ-01,TJ-12 | Planned |
| R-TJ-003 | هوية TestCaseAttempt مستقلة لكل retry/parameterization/parallel test | T186,T188 | TJ-05 | Planned |
| R-TJ-004 | استقبال xUnit/NUnit/MSTest وTRX/partial test results دون ادعاء Trace من النتيجة وحدها | T187 | TJ-04,TJ-05 | Planned |
| R-TJ-005 | إثبات تسلسل دخول الدوال واستدعاءاتها بمراجع Runtime Span | T188,T189,T193 | TJ-02 | Planned |
| R-TJ-006 | إظهار Return/Throw/TaskCompletion بعد Instrumentation لا من مجرد Coverage | T190,T192,T200 | TJ-02,TJ-04 | Planned |
| R-TJ-007 | نوع وقيمة المدخلات والمخرجات إذا جُمعت بإذن، وNotCaptured إذا لم تجمع | T191,T200 | TJ-03,TJ-13 | Planned |
| R-TJ-008 | ربط الاستثناءات وAssertions وExpected/Actual بنوع المصدر والثقة | T192,T200 | TJ-04,TJ-09 | Planned |
| R-TJ-009 | تتبع فروع async/parallel/distributed بحسب Causality وليس ترتيب الساعة | T188,T194 | TJ-05,TJ-06 | Planned |
| R-TJ-010 | دعم OTEL/ActivitySource standard instrumentations بنطاق test attempt | T188,T189 | TJ-02,TJ-11 | Planned |
| R-TJ-011 | ملفات Deep Method tracing اختيارية وذات حدود تكلفة ووقت | T190,T203 | TJ-02,TJ-18 | Planned |
| R-TJ-012 | ربط كل Invocation بSymbol/Source/BuildSHA أو OutOfRevision/Unresolved | T193 | TJ-02,TJ-10 | Planned |
| R-TJ-013 | تمييز Unsupported/Sampled/Uninstrumented/Partial trace عن عدم حدوث الاستدعاء | T189,T195,T196 | TJ-11,TJ-15 | Planned |
| R-TJ-014 | Overlay لمسار الاختبار على عقد المخطط الأصلي بلا صناديق TestCase منفصلة | T198 | TJ-07,TJ-08 | Planned |
| R-TJ-015 | فتح Flow Diagram خاص باختبار بعينه على الطلب مع Sequence/Observed Flow | T199 | TJ-07 | Planned |
| R-TJ-016 | Timeline تدعم Play/Pause/Step/Jump Failure مع Source/Return inspector | T196,T197,T200 | TJ-08,TJ-09,TJ-15 | Planned |
| R-TJ-017 | Mermaid صالح لمسار الاختبار مع وسم Unknown وحماية البيانات | T199,T203 | TJ-07,TJ-12 | Planned |
| R-TJ-018 | مقارنة مساري اختبار عند Commit أو AET Candidate مختلف وقياس comparability | T201 | TJ-17 | Planned |
| R-TJ-019 | البيانات الخام الحساسة مطفأة افتراضيًا، وRedaction+RBAC قبل التخزين/التصدير | T191,T203 | TJ-12,TJ-13 | Planned |
| R-TJ-020 | Crash-safe durable replay وفحص الآثار واستئناف وكيل آخر دون تكرار غير آمن | T196,T204 | TJ-14,TJ-15 | Planned |
| R-TJ-021 | محادثة AI عن Step محدد عبر MCP مع RunID/StepID وإرجاع التركيز للرسم | T202 | TJ-16 | Planned |
| R-TJ-022 | Benchmarks للمسارات الكبيرة وتحديد Coverage الصحيح وقيود المراقبة | T195,T205 | TJ-11,TJ-18 | Planned |
