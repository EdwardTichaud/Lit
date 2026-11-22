# Annexe de l’audit de progression

Instantané du 22 novembre 2026. Les identifiants sont conservés pour retrouver précisément les raccords. « Pas de référence directe » signifie candidat à examiner, pas preuve d’inutilité : une recette, un chargement par identifiant ou un script peut constituer un autre chemin.

Les passages 09 et 10 des Limbes ont été raccordés le 22 novembre 2026 dans la scène Secrets ; leur ligne ci-dessous reflète cette correction. Le contrôle est statique, sans parcours Play Mode.

Les trois identifiants en doublon du constat B15 ont été corrigés le 22 novembre 2026 ; le tableau des objets ci-dessous intègre ces nouveaux identifiants. Les autres données restent celles de l’audit initial.

## Les dix-huit cycles et leurs cent douze étapes

### district1.ardent — La voix retenue

Classement : principal. Définition : [ArdentCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/ArdentCycle.asset:1). Scène retirée après résolution : District_1_Cycle_Ardent.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| conduits_entered | ZoneEntered: conduits_entry | — |  | — |
| lucian_conduits_entry | DialogueCompleted: lucian_conduits_entry | conduits_entered |  | — |
| transfer_register_read | Knowledge: Knowledge_Ardent_transfer_register_read | conduits_entered |  | — |
| conduit_instruction_read | Knowledge: Knowledge_Ardent_conduit_instruction_read | conduits_entered |  | — |
| iris_ribbon_found | Knowledge: Knowledge_Ardent_iris_ribbon_found | conduits_entered |  | — |
| echo_relay_one_tuned | Interaction: echo_relay_one_tuned | transfer_register_read ET conduit_instruction_read ET iris_ribbon_found |  | — |
| echo_relay_two_tuned | Interaction: echo_relay_two_tuned | transfer_register_read ET conduit_instruction_read ET iris_ribbon_found |  | — |
| echo_relay_three_tuned | Interaction: echo_relay_three_tuned | transfer_register_read ET conduit_instruction_read ET iris_ribbon_found |  | — |
| false_choir_defeated | EnemyDefeated: false_choir | echo_relay_one_tuned ET echo_relay_two_tuned ET echo_relay_three_tuned |  | — |
| voice_departure_seen | SequenceCompleted: voice_departure | false_choir_defeated |  | Knowledge: Knowledge_Ardent_iris_departure_understood |
| nora_spoken | DialogueCompleted: nora | voice_departure_seen |  | Knowledge: Knowledge_Ardent_nora_wait_released; Activation: district1.ardent.lower_procession_access |
| lucian_after_ardent | DialogueCompleted: lucian_after_ardent | nora_spoken | oui | — |

### district1.belmont — Le tour de la Veillée

Classement : principal. Définition : [BelmontCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/BelmontCycle.asset:1). Scène retirée après résolution : District_1_Cycle_Belmont.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| shift_register_read | Knowledge: Knowledge_Belmont_shift_register | — |  | — |
| family_note_read | Knowledge: Knowledge_Belmont_family_note | — |  | — |
| flame_maintenance_read | Knowledge: Knowledge_Belmont_flame_maintenance | — |  | — |
| eloise_spoken | DialogueCompleted: eloise | shift_register_read ET family_note_read ET flame_maintenance_read |  | Knowledge: Knowledge_Belmont_duty_understood |
| shadow_warden_defeated | EnemyDefeated: shadow_warden | eloise_spoken |  | Activation: district1.belmont.crypt_access |
| lucian_after_shadow | DialogueCompleted: lucian_after_shadow | shadow_warden_defeated |  | — |
| lower_flame_one_lit | Interaction: lower_flame_one_lit | lucian_after_shadow |  | — |
| lower_flame_two_lit | Interaction: lower_flame_two_lit | lucian_after_shadow |  | — |
| lower_flame_three_lit | Interaction: lower_flame_three_lit | lucian_after_shadow |  | — |
| lower_flames_restored | SequenceCompleted: anchor_release | lower_flame_one_lit ET lower_flame_two_lit ET lower_flame_three_lit |  | — |
| first_ancient_flame_lit | Interaction: first_ancient_flame | lower_flames_restored | oui | Knowledge: Knowledge_Belmont_first_ancient_flame; Activation: district1.first_ancient_flame.lit |

### district1.clairmont — Le registre ouvert

Classement : principal. Définition : [ClairmontCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/ClairmontCycle.asset:1). Scène retirée après résolution : District_1_Cycle_Clairmont.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| nef_entered | ZoneEntered: nef_entry | — |  | — |
| lucian_nef_entry | DialogueCompleted: lucian_nef_entry | nef_entered |  | — |
| relief_register_read | Knowledge: Knowledge_Clairmont_relief_register | nef_entered |  | — |
| old_route_read | Knowledge: Knowledge_Clairmont_old_route | nef_entered |  | — |
| register_consulted | DialogueCompleted: register | relief_register_read ET old_route_read |  | Knowledge: Knowledge_Clairmont_register_understood |
| agnes_spoken | DialogueCompleted: agnes | register_consulted |  | Knowledge: Knowledge_Clairmont_agnes_released; Activation: district1.clairmont.statue_belvedere_access |
| lucian_after_clairmont | DialogueCompleted: lucian_after_clairmont | agnes_spoken | oui | — |

### district1.etienne — La dernière relève

Classement : principal. Définition : [EtienneCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/EtienneCycle.asset:1). Scène retirée après résolution : District_1_Cycle_Etienne.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| puits_entered | ZoneEntered: puits_entry | — |  | — |
| lucian_puits_entry | DialogueCompleted: lucian_puits_entry | puits_entered |  | — |
| relief_register_read | Knowledge: Knowledge_ReliefPassed | puits_entered |  | — |
| conduit_route_read | Knowledge: Knowledge_ConduitRoute | puits_entered |  | — |
| emergency_brake_read | Knowledge: Knowledge_EmergencyBrake | puits_entered |  | — |
| brake_released | Interaction: emergency_brake | relief_register_read ET conduit_route_read ET emergency_brake_read |  | — |
| dead_weight_defeated | EnemyDefeated: dead_weight | brake_released |  | — |
| last_relief_seen | SequenceCompleted: last_relief | dead_weight_defeated |  | Knowledge: Knowledge_LastRelief |
| etienne_spoken | DialogueCompleted: etienne | last_relief_seen ET relief_register_read ET conduit_route_read ET emergency_brake_read |  | Knowledge: Knowledge_EtienneAtPeace; Activation: district1.etienne.flooded_conduits_access |
| lucian_after_etienne | DialogueCompleted: lucian_after_etienne | etienne_spoken | oui | — |

### district1.luc — Le souvenir de Luc

Classement : annexe. Définition : [LucCycle.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Luc/LucCycle.asset:1). Scène retirée après résolution : District_1_Cycle_Luc.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| jon_collar_collected | Knowledge: Knowledge_JonLocationClue_1 | — |  | — |
| jon_grave_read | Knowledge: Knowledge_JonLocationClue_2 | — |  | — |
| luc_spoken | Interaction: luc | jon_collar_collected ET jon_grave_read |  | — |
| luc_farewell | DialogueCompleted: luc | luc_spoken | oui | Knowledge: Knowledge_JonLocation |

### district1.nina — Le souvenir de Nina

Classement : annexe. Définition : [NinaCycle.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Nina/NinaCycle.asset:1). Scène retirée après résolution : District_1_Cycle_Nina.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| chimeras_known | Knowledge: Knowledge_ExistenceDesChimeres | — |  | — |
| edouard_understood | Knowledge: Knowledge_DilemmeEdouard | — |  | — |
| scientist_defeated | EnemyDefeated: encounter | — |  | Knowledge: Knowledge_ExistenceDesChimeres |
| nina_spoken | Interaction: nina | chimeras_known ET edouard_understood |  | — |
| scar_reward | DialogueCompleted: scar | nina_spoken | oui | Skill: Skill_3_Cicatrice |
| aftermath_seen | SequenceCompleted: cinematic | scientist_defeated |  | — |

### district2.courts.family — Les portes qu'on laisse ouvertes

Classement : principal. Définition : [Courts_familyCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/Courts_familyCycle.asset:1). Scène retirée après résolution : aucune scène dédiée.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| curiosity | Knowledge: Knowledge_Courts_familyCycle_curiosity | — |  | — |
| ties | Knowledge: Knowledge_Courts_familyCycle_ties | — |  | — |
| help | Knowledge: Knowledge_Courts_familyCycle_help | — |  | — |
| memory_curiosity | NamedFact: curiosity_memory | curiosity |  | — |
| memory_ties | NamedFact: ties_memory | ties |  | — |
| memory_help | NamedFact: help_memory | help |  | — |
| restored | Interaction: restore | memory_curiosity ET memory_ties ET memory_help | oui | — |

### district2.courts.finale — Le passage commun

Classement : principal. Définition : [Courts_finaleCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/Courts_finaleCycle.asset:1). Scène retirée après résolution : aucune scène dédiée.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| introduced | NamedFact: introduction | — |  | — |
| defeated | EnemyDefeated: gargoyle | introduced |  | — |
| concluded | NamedFact: conclusion | defeated | oui | — |

### district2.courts.margins — Les marges du plan

Classement : annexe. Définition : [Courts_marginsCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/Courts_marginsCycle.asset:1). Scène retirée après résolution : aucune scène dédiée.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| old | Knowledge: Knowledge_Courts_marginsCycle_old | — |  | — |
| made | Knowledge: Knowledge_Courts_marginsCycle_made | — |  | — |
| compare | DialogueCompleted: compare | old ET made | oui | — |

### district2.courts.reserve — La réserve commune

Classement : annexe. Définition : [Courts_reserveCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/Courts_reserveCycle.asset:1). Scène retirée après résolution : aucune scène dédiée.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| baskets | Knowledge: Knowledge_Courts_reserveCycle_baskets | — |  | — |
| ledger | Knowledge: Knowledge_Courts_reserveCycle_ledger | — |  | — |
| shared | DialogueCompleted: shared | baskets ET ledger | oui | — |

### district2.courts.visits — Le trajet des visites

Classement : annexe. Définition : [Courts_visitsCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/Courts_visitsCycle.asset:1). Scène retirée après résolution : aucune scène dédiée.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| silas | Knowledge: Knowledge_Courts_visitsCycle_silas | — |  | — |
| visits | Knowledge: Knowledge_Courts_visitsCycle_visits | — |  | — |
| return | DialogueCompleted: visits_return | silas ET visits | oui | — |

### global.secret_notes — Les vingt notes des passages

Classement : annexe. Définition : [SecretNotesCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/SecretNotesCycle.asset:1). Scène retirée après résolution : aucune scène dédiée.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| note_01 | Read: secret.note.01 | — | oui | — |
| note_02 | Read: secret.note.02 | — | oui | — |
| note_03 | Read: secret.note.03 | — | oui | — |
| note_04 | Read: secret.note.04 | — | oui | — |
| note_05 | Read: secret.note.05 | — | oui | — |
| note_06 | Read: secret.note.06 | — | oui | — |
| note_07 | Read: secret.note.07 | — | oui | — |
| note_08 | Read: secret.note.08 | — | oui | — |
| note_09 | Read: secret.note.09 | — | oui | — |
| note_10 | Read: secret.note.10 | — | oui | — |
| note_11 | Read: secret.note.11 | — | oui | — |
| note_12 | Read: secret.note.12 | — | oui | — |
| note_13 | Read: secret.note.13 | — | oui | — |
| note_14 | Read: secret.note.14 | — | oui | — |
| note_15 | Read: secret.note.15 | — | oui | — |
| note_16 | Read: secret.note.16 | — | oui | — |
| note_17 | Read: secret.note.17 | — | oui | — |
| note_18 | Read: secret.note.18 | — | oui | — |
| note_19 | Read: secret.note.19 | — | oui | — |
| note_20 | Read: secret.note.20 | — | oui | — |

### reve.limbes.ange — Le dernier passage

Classement : principal. Définition : [LimbesAngelCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/LimbesAngelCycle.asset:1). Scène retirée après résolution : aucune scène dédiée.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| introduced | NamedFact: introduction | — |  | — |
| defeated | NamedFact: angel_defeat | introduced |  | — |
| departed | NamedFact: departure | defeated | oui | — |

### reve.limbes.captivite — La lignée captive

Classement : principal. Définition : [LimbesCaptiviteCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/LimbesCaptiviteCycle.asset:1). Scène retirée après résolution : Reve_Limbes_Cycle_Captivite.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| proof_0 | Knowledge: Knowledge_Captivite_0 | — |  | — |
| proof_1 | Knowledge: Knowledge_Captivite_1 | — |  | — |
| proof_2 | Knowledge: Knowledge_Captivite_2 | — |  | — |
| understood | NamedFact: memory | proof_0 ET proof_1 ET proof_2 | oui | — |

### reve.limbes.chambre — La chambre à l'envers

Classement : annexe. Définition : [LimbesChambreCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/LimbesChambreCycle.asset:1). Scène retirée après résolution : Reve_Limbes_Cycle_Chambre.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| entered | ZoneEntered: entry | — |  | — |
| fragment_0 | Knowledge: Knowledge_0 | entered |  | — |
| resolved | DialogueCompleted: resolve | fragment_0 | oui | Activation: reve.limbes.chambre_apaisee; Item: Item_Chiffon sale |

### reve.limbes.creation — Le monde inachevé

Classement : principal. Définition : [LimbesCreationCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/LimbesCreationCycle.asset:1). Scène retirée après résolution : Reve_Limbes_Cycle_Creation.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| proof_0 | Knowledge: Knowledge_Creation_0 | — |  | — |
| proof_1 | Knowledge: Knowledge_Creation_1 | — |  | — |
| proof_2 | Knowledge: Knowledge_Creation_2 | — |  | — |
| understood | NamedFact: memory | proof_0 ET proof_1 ET proof_2 | oui | — |

### reve.limbes.invocation — Le visage de la statue

Classement : principal. Définition : [LimbesInvocationCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/LimbesInvocationCycle.asset:1). Scène retirée après résolution : Reve_Limbes_Cycle_Invocation.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| proof_0 | Knowledge: Knowledge_Invocation_0 | — |  | — |
| proof_1 | Knowledge: Knowledge_Invocation_1 | — |  | — |
| proof_2 | Knowledge: Knowledge_Invocation_2 | — |  | — |
| understood | NamedFact: memory | proof_0 ET proof_1 ET proof_2 | oui | — |

### reve.limbes.seuil — Le seuil éparpillé

Classement : principal. Définition : [LimbesSeuilCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/LimbesSeuilCycle.asset:1). Scène retirée après résolution : Reve_Limbes_Cycle_Seuil.

| Étape | Événement/source | Prérequis | Fin | Récompense |
|---|---|---|---|---|
| entered | ZoneEntered: entry | — |  | — |
| fragment_0 | Knowledge: Knowledge_0 | entered |  | — |
| fragment_1 | Knowledge: Knowledge_1 | entered |  | — |
| fragment_2 | Knowledge: Knowledge_2 | entered |  | — |
| resolved | DialogueCompleted: resolve | fragment_0 ET fragment_1 ET fragment_2 | oui | Activation: reve.limbes.seuil_recompose |

## Capacités d’exploration

| Capacité | Acquisition réelle | Accès identifiés |
|---|---|---|
| Observateur | Départ | Découverte des passages via d6/notes ; repérage des prises. |
| Saut de lumière | Départ | LadderController, trois raccords District 1 et un dans les Limbes. |
| Grimpeur | LimbesSeuilCycle.entered | ClingPoint limbes.seuil.poutre dans les secrets des Limbes ; pas une résolution complète du Seuil. |
| Sauf-Conduit | Belmont terminé | Huit TrouEtroit reliés au catalogue, plus le raccord historique de la nef. |

## Les seize emplacements de techniques

| Personnage | Emplacement | Technique | Nouvelle partie solo | Source ultérieure |
|---|---|---|---|---|
| lucian | 1 | Skill_BondOffensif | ouvert | événement de campagne non raccordé pour cet emplacement fermé |
| lucian | 2 | Skill_1_Eclair | ouvert | événement de campagne non raccordé pour cet emplacement fermé |
| lucian | 3 | Skill_4_JumpKick_Rupture | ouvert | événement de campagne non raccordé pour cet emplacement fermé |
| lucian | 4 | Skill_ImpactDeLumiere | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| vesta | 1 | Skill_Revitalisation | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| vesta | 2 | Skill_SoinsUrgence | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| vesta | 3 | Skill_Regeneration | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| vesta | 4 | Skill_SiphonVital | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| soren | 1 | Skill_2_Fleche de lumière | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| soren | 2 | Skill_Revelation | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| soren | 3 | Skill_Entrave | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| soren | 4 | Skill_PasDeRecul | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| aurell | 1 | Skill_Intercession | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| aurell | 2 | Skill_Rempart | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| aurell | 3 | Skill_Sommation | fermé | événement de campagne non raccordé pour cet emplacement fermé |
| aurell | 4 | Skill_JusteRetour | fermé | événement de campagne non raccordé pour cet emplacement fermé |

Cicatrice est une récompense supplémentaire de Nina, absente de ces seize références. Le catalogue ne suffit donc pas à fournir un accès gamepad à toutes les compétences connues.

## Les vingt identités de notes et leurs passages

| Note | Passage | Note placée selon catalogue | Passage déclaré | Raccord actuel |
|---|---|---|---|---|
| 1 | Passage_01 | District_2_Courts_Secrets | District_1_AbilityShortcuts | KnowledgePassageDiscovery présent |
| 2 | Passage_02 | Reve_Limbes_Secrets | District_1_SecretNotes | KnowledgePassageDiscovery présent |
| 3 | Passage_03 | District_2_Courts_Secrets | District_2_Courts_Secrets | KnowledgePassageDiscovery présent |
| 4 | Passage_04 | District_2_Courts_Secrets | District_2_Courts_Secrets | KnowledgePassageDiscovery présent |
| 5 | Passage_05 | District_2_Courts_Secrets | District_2_Courts_Secrets | KnowledgePassageDiscovery présent |
| 6 | Passage_06 | District_2_Courts_Secrets | District_2_Courts_Secrets | KnowledgePassageDiscovery présent |
| 7 | Passage_07 | District_1_SecretNotes | District_2_Courts_Secrets | KnowledgePassageDiscovery présent |
| 8 | Passage_08 | Reve_Limbes_Secrets | District_2_Courts_Secrets | KnowledgePassageDiscovery présent |
| 9 | Passage_09 | District_2_Courts_Secrets | Reve_Limbes_Secrets | KnowledgePassageDiscovery présent ; raccord rétabli le 22 novembre |
| 10 | Passage_10 | Reve_Limbes_Secrets | Reve_Limbes_Secrets | KnowledgePassageDiscovery présent ; raccord rétabli le 22 novembre |
| 11 |  |  |  | réservé, contenu absent |
| 12 |  |  |  | réservé, contenu absent |
| 13 |  |  |  | réservé, contenu absent |
| 14 |  |  |  | réservé, contenu absent |
| 15 |  |  |  | réservé, contenu absent |
| 16 |  |  |  | réservé, contenu absent |
| 17 |  |  |  | réservé, contenu absent |
| 18 |  |  |  | réservé, contenu absent |
| 19 |  |  |  | réservé, contenu absent |
| 20 |  |  |  | réservé, contenu absent |

## Savoirs et sources de progression

Les sources ci-dessous sont les attributions déclarées dans les données. Une référence d’évidence, de présentation ou de prérequis ne compte pas comme attribution. Les sources scriptées sont ajoutées pour le prélude, les Archives et Enola. Les étapes consommatrices ne listent pas tous les effets de dialogue, de découverte ou les prérequis hors CycleStep.

| Savoir | Source déclarée | Étapes Knowledge consommatrices |
|---|---|---|
| auCommencement — Au commencement | LocalVoiceLineController, Maison | aucune ; peut rester un savoir narratif/local |
| belmont.duty_understood — Le tour de la Veillée | district1.belmont:eloise_spoken; réaction ghost_eloise_belmont | aucune ; peut rester un savoir narratif/local |
| belmont.family_note — Le choix d’Étienne | Note d’Étienne (lecture) | district1.belmont:family_note_read |
| belmont.first_ancient_flame — La première Ancient Flame | district1.belmont:first_ancient_flame_lit | aucune ; peut rester un savoir narratif/local |
| belmont.flame_maintenance — Les conduits inférieurs | Relevé des conduits (lecture) | district1.belmont:flame_maintenance_read |
| belmont.lower_flame_network — Le réseau de Veillée | aucune attribution identifiée | aucune ; peut rester un savoir narratif/local |
| belmont.shift_register — La relève d’Étienne | Registre de relève (lecture) | district1.belmont:shift_register_read |
| courts.optional.visit — Une visite sans panier | Une visite sans panier (lecture) | aucune ; peut rester un savoir narratif/local |
| district1.ardent.conduit_instruction_read — Les conduits transmettent des voix lointaines | Consigne de maintenance des conduits (lecture); Consigne de maintenance des conduits (ramassage) | district1.ardent:conduit_instruction_read |
| district1.ardent.iris_departure_understood — La voix persiste après le départ d’Iris | district1.ardent:voice_departure_seen | aucune ; peut rester un savoir narratif/local |
| district1.ardent.iris_ribbon_found — Le ruban transmis à Iris | Ruban familial d’Iris (lecture); Ruban familial d’Iris (ramassage) | district1.ardent:iris_ribbon_found |
| district1.ardent.nora_wait_released — Nora cesse d’attendre derrière la porte | district1.ardent:nora_spoken | aucune ; peut rester un savoir narratif/local |
| district1.ardent.transfer_register_read — Iris a quitté les conduits | Registre de transfert d’Iris (lecture); Registre de transfert d’Iris (ramassage) | district1.ardent:transfer_register_read |
| district1.clairmont.agnes_released — Le registre peut être clos | district1.clairmont:agnes_spoken | aucune ; peut rester un savoir narratif/local |
| district1.clairmont.old_route — Une circulation plus ancienne | Consigne du parcours ancien (lecture); Consigne du parcours ancien (ramassage) | district1.clairmont:old_route_read |
| district1.clairmont.register_understood — Un relevé de départ, pas de retour | district1.clairmont:register_consulted | aucune ; peut rester un savoir narratif/local |
| district1.clairmont.relief_register — La relève a reçu les noms | Bordereau de transmission (lecture); Bordereau de transmission (ramassage) | district1.clairmont:relief_register_read |
| district1.etienne.ConduitRoute — Un réseau dirigé sous la statue | Relevé des conduits de Veillée (lecture); Relevé des conduits de Veillée (ramassage) | district1.etienne:conduit_route_read |
| district1.etienne.EmergencyBrake — Tenir le frein pour les autres | Note du frein de secours (lecture); Note du frein de secours (ramassage) | district1.etienne:emergency_brake_read |
| district1.etienne.EtienneAtPeace — Étienne peut lâcher | district1.etienne:etienne_spoken | aucune ; peut rester un savoir narratif/local |
| district1.etienne.LastRelief — Le dernier geste d'Étienne | district1.etienne:last_relief_seen | aucune ; peut rester un savoir narratif/local |
| district1.etienne.ReliefPassed — La relève a franchi le passage | Registre de passage de la relève (lecture); Registre de passage de la relève (ramassage) | district1.etienne:relief_register_read |
| district2.courts.family.curiosity — Le carnet de Alix | Le carnet de Alix (lecture) | district2.courts.family:curiosity |
| district2.courts.family.help — Les allers-retours | Les allers-retours (lecture) | district2.courts.family:help |
| district2.courts.family.ties — Le billet d'Agathe | Le billet d'Agathe (lecture) | district2.courts.family:ties |
| district2.courts.margins.made — Les pierres dégagées | Les pierres dégagées (lecture) | district2.courts.margins:made |
| district2.courts.margins.old — L'ouverture ancienne | L'ouverture ancienne (lecture) | district2.courts.margins:old |
| district2.courts.reserve.baskets — Les paniers de la réserve | Les paniers de la réserve (lecture) | district2.courts.reserve:baskets |
| district2.courts.reserve.ledger — Le registre des parts | Le registre des parts (lecture) | district2.courts.reserve:ledger |
| district2.courts.visits.silas — Le trajet de Abel | Le trajet de Abel (lecture) | district2.courts.visits:silas |
| district2.courts.visits.visits — Deux visites ordinaires | Deux visites ordinaires (lecture) | district2.courts.visits:visits |
| enola.knife.rhythm — La cadence d'Enola | victoire EnolaKnifeGame, solo | aucune ; peut rester un savoir narratif/local |
| f2cc3ef506d28b24caeceb3f4a102432 — Les symboles numérotés des Archives | trois indices des Archives, FractureGraybox | aucune ; peut rester un savoir narratif/local |
| jonLocation — Le destin de Jon | district1.luc:luc_farewell; réaction ghost_luc | aucune ; peut rester un savoir narratif/local |
| jonLocationClue_1 — Le collier de Jon | Collier de Jon (ramassage); Collier (lecture); Note (lecture) | district1.luc:jon_collar_collected |
| jonLocationClue_2 — La tombe a la clochette | Petite tombe de Jon (lecture) | district1.luc:jon_grave_read |
| nina.DilemmeEdouard — Dilemme Édouard | Lettre manuscrite d'Édouard (lecture) | district1.nina:edouard_understood |
| nina.ExistenceDesChimeres — Existence des chimères | district1.nina:scientist_defeated | district1.nina:chimeras_known |
| reve.limbes.captivite.proof.0 — Des cellules sous les chapelles | Des cellules sous les chapelles (lecture); Des cellules sous les chapelles (ramassage) | reve.limbes.captivite:proof_0 |
| reve.limbes.captivite.proof.1 — La lignée avant les noms | La lignée avant les noms (lecture); La lignée avant les noms (ramassage) | reve.limbes.captivite:proof_1 |
| reve.limbes.captivite.proof.2 — Des âmes venues des Enfers | Des âmes venues des Enfers (lecture); Des âmes venues des Enfers (ramassage) | reve.limbes.captivite:proof_2 |
| reve.limbes.chambre.fragment.0 — La fenêtre n'a pas bougé | La fenêtre n'a pas bougé (lecture); La fenêtre n'a pas bougé (ramassage) | reve.limbes.chambre:fragment_0 |
| reve.limbes.creation.proof.0 — Les deux faces d'une arche | Les deux faces d'une arche (lecture); Les deux faces d'une arche (ramassage) | reve.limbes.creation:proof_0 |
| reve.limbes.creation.proof.1 — Le nom d'Azazel | Le nom d'Azazel (lecture); Le nom d'Azazel (ramassage) | reve.limbes.creation:proof_1 |
| reve.limbes.creation.proof.2 — Une création imparfaite | Une création imparfaite (lecture); Une création imparfaite (ramassage) | reve.limbes.creation:proof_2 |
| reve.limbes.invocation.proof.0 — Le visage sous la protection | Le visage sous la protection (lecture); Le visage sous la protection (ramassage) | reve.limbes.invocation:proof_0 |
| reve.limbes.invocation.proof.1 — Les voix réunies | Les voix réunies (lecture); Les voix réunies (ramassage) | reve.limbes.invocation:proof_1 |
| reve.limbes.invocation.proof.2 — Le passage interrompu | Le passage interrompu (lecture); Le passage interrompu (ramassage) | reve.limbes.invocation:proof_2 |
| reve.limbes.seuil.fragment.0 — Un seuil sans mur | Un seuil sans mur (lecture); Un seuil sans mur (ramassage) | reve.limbes.seuil:fragment_0 |
| reve.limbes.seuil.fragment.1 — La même entaille | La même entaille (lecture); La même entaille (ramassage) | reve.limbes.seuil:fragment_1 |
| reve.limbes.seuil.fragment.2 — Des pierres qui se reconnaissent | Des pierres qui se reconnaissent (lecture); Des pierres qui se reconnaissent (ramassage) | reve.limbes.seuil:fragment_2 |
| secret.passage.01 — District 1 — le couloir ouest | Note — District 1 — le couloir ouest (lecture) | aucune ; peut rester un savoir narratif/local |
| secret.passage.02 — District 1 — l'entrée et le couloir ouest | Note — District 1 — l'entrée et le couloir ouest (lecture) | aucune ; peut rester un savoir narratif/local |
| secret.passage.03 — Cours — Maison et galerie | Note — Cours — Maison et galerie (lecture) | aucune ; peut rester un savoir narratif/local |
| secret.passage.04 — Cours — Atelier et galerie | Note — Cours — Atelier et galerie (lecture) | aucune ; peut rester un savoir narratif/local |
| secret.passage.05 — Cours — D'une maison à l'autre | Note — Cours — D'une maison à l'autre (lecture) | aucune ; peut rester un savoir narratif/local |
| secret.passage.06 — Cours — La cour et la maison | Note — Cours — La cour et la maison (lecture) | aucune ; peut rester un savoir narratif/local |
| secret.passage.07 — Cours — La cour et l'atelier | Note — Cours — La cour et l'atelier (lecture) | aucune ; peut rester un savoir narratif/local |
| secret.passage.08 — Cours — Le recoin de la réserve | Note — Cours — Le recoin de la réserve (lecture) | aucune ; peut rester un savoir narratif/local |
| secret.passage.09 — Limbes — vers la terrasse gauche | Note — Limbes — vers la terrasse gauche (lecture) | aucune ; peut rester un savoir narratif/local |
| secret.passage.10 — Limbes — vers la terrasse droite | Note — Limbes — vers la terrasse droite (lecture) | aucune ; peut rester un savoir narratif/local |
| ulricRemainsLocation — Ulric Remains Location | Note (lecture) | aucune ; peut rester un savoir narratif/local |

## Scènes chargées par chaque manifeste

### District_1_Core

Manifeste : [ZoneManifest_District_1.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/ZoneManifest_District_1.asset:1).

- [District_1_Core.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Core.unity:1)
- [District_1_Rooms_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Rooms_Environment.unity:1)
- [District_1_Corridor_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Corridor_Environment.unity:1)
- [District_1_Crypt_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Crypt_Environment.unity:1)
- [District_1_Arena_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Arena_Environment.unity:1)
- [District_1_Balcony_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Balcony_Environment.unity:1)
- [District_1_Environment_OldBarn.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Environment_OldBarn.unity:1)
- [District_1_Indoor_Lights.unity](C:/Users/pierr/git/Lit/Assets/District_1_Indoor_Lights.unity:1)
- [District_1_Interactives.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Interactives.unity:1)
- [District_1_Corridor_Flammes.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Corridor_Flammes.unity:1)
- [District_1_Rooms_Flammes.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Rooms_Flammes.unity:1)
- [District_1_Cycle_Luc.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Luc/District_1_Cycle_Luc.unity:1)
- [District_1_Cycle_Gustave.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Cycle_Gustave.unity:1)
- [District_1_Enigme_Levers.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Enigme_Levers.unity:1)
- [District_1_Enemies.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Enemies.unity:1)
- [District_1_Sequences.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Sequences.unity:1)
- [District_1_Cycle_Nina.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Nina/District_1_Cycle_Nina.unity:1)
- [District_1_Cycle_Belmont.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/District_1_Cycle_Belmont.unity:1)
- [District_1_PuitsDeLaReleve_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_PuitsDeLaReleve_Environment.unity:1)
- [District_1_Cycle_Etienne.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Etienne/District_1_Cycle_Etienne.unity:1)
- [District_1_ConduitsNoyés_Environement.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_ConduitsNoyés_Environement.unity:1)
- [District_1_Cycle_Ardent.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Ardent/District_1_Cycle_Ardent.unity:1)
- [District_1_NefDesToursInterrompus_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_NefDesToursInterrompus_Environment.unity:1)
- [District_1_Cycle_Clairmont.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Clairmont/District_1_Cycle_Clairmont.unity:1)
- [District_1_AbilityShortcuts.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_AbilityShortcuts.unity:1)
- [District_1_SecretNotes.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_SecretNotes.unity:1)
- [District_1_Fracture_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Fracture_Environment.unity:1)

### District_2_Courts

Manifeste : [ZoneManifest_District_2_Courts.asset](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/ZoneManifest_District_2_Courts.asset:1).

- [District_2_Courts.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/District_2_Courts.unity:1)
- [District_2_Courts_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/District_2_Courts_Environment.unity:1)
- [District_2_Courts_Secrets.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/District_2_Courts_Secrets.unity:1)

### Fracture_Cycle_Enola

Manifeste : [ZoneManifest_Enola.asset](C:/Users/pierr/git/Lit/Assets/Gameplay/EnolaKnifeGame/ZoneManifest_Enola.asset:1).

- [Fracture_Cycle_Enola.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Fracture_Cycle_Enola.unity:1)

### Maison_Core

Manifeste : [ZoneManifest_Maison.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/ZoneManifest_Maison.asset:1).

- [Maison_Core.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Core.unity:1)
- [Maison_Lights.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Lights.unity:1)
- [Maison_Interactives.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Interactives.unity:1)
- [Maison_Environment_Near_1.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Environment_Near_1.unity:1)
- [Maison_Environment_Near_2.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Environment_Near_2.unity:1)
- [Maison_Environment_Near_3.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Environment_Near_3.unity:1)
- [Maison_Environment_Near_4.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Environment_Near_4.unity:1)
- [Maison_Environment_Far.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Environment_Far.unity:1)
- [Maison_Sequences.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Sequences.unity:1)

### Reve_Limbes

Manifeste : [ZoneManifest_Reve_Limbes.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset:1).

- [Reve_Limbes.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes.unity:1)
- [Reve_Limbes_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Environment.unity:1)
- [Reve_Limbes_Core.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Core.unity:1)
- [Reve_Limbes_Interactives.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Interactives.unity:1)
- [Reve_Limbes_Cycle_Seuil.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Cycle_Seuil.unity:1)
- [Reve_Limbes_Cycle_Chambre.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Cycle_Chambre.unity:1)
- [Reve_Limbes_Progression.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Progression.unity:1)
- [Reve_Limbes_Cycle_Creation.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Cycle_Creation.unity:1)
- [Reve_Limbes_Cycle_Captivite.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Cycle_Captivite.unity:1)
- [Reve_Limbes_Cycle_Invocation.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Cycle_Invocation.unity:1)
- [Reve_Limbes_Secrets.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Secrets.unity:1)

## Scènes de réserve et laboratoires hors des manifestes

| Scène | Statut observable |
|---|---|
| [AngelOfJustice.unity](C:/Users/pierr/git/Lit/Assets/Scenes/AngelOfJustice.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [Arena.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Arena.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [District_3.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_3.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [District_4.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_4.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [District_5.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_5.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [Outdoor.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Outdoor.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [SampleScene.unity](C:/Users/pierr/git/Lit/Assets/Scenes/SampleScene.unity:1) | échantillon ; ne pas compter comme région livrée |
| [CommonAreas.unity](C:/Users/pierr/git/Lit/Assets/Scenes/CommonAreas/CommonAreas.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [District_2_Environment_WhiteCastle.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/District_2_Environment_WhiteCastle.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [District_3_Cathedrale.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_3_Cathedrale/District_3_Cathedrale.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [Maison_AccesReve.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_AccesReve.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [Maison_Enigme_Vigil.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Enigme_Vigil.unity:1) | pas de manifeste ni de destination de campagne identifiée |
| [UccExplorationCourse.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Tests/UccExplorationCourse.unity:1) | laboratoire/prototype ; absence de campagne cohérente |
| [AnimationLab.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Workshop/AnimationLab.unity:1) | laboratoire/prototype ; absence de campagne cohérente |
| [CinematicLab.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Workshop/CinematicLab.unity:1) | laboratoire/prototype ; absence de campagne cohérente |
| [Fracture_Core.unity](C:/Users/pierr/git/Lit/Assets/Gameplay/FractureLabyrinth/Prototype/Fracture_Core.unity:1) | laboratoire/prototype ; absence de campagne cohérente |
| [Fracture_Laby.unity](C:/Users/pierr/git/Lit/Assets/Gameplay/FractureLabyrinth/Prototype/Fracture_Laby.unity:1) | laboratoire/prototype ; absence de campagne cohérente |
| [Iluvilirae_Test.unity](C:/Users/pierr/git/Lit/Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test.unity:1) | laboratoire ou système historique, hors campagne |
| [Iluvilirae_Perception_Test.unity](C:/Users/pierr/git/Lit/Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Perception_Test.unity:1) | laboratoire ou système historique, hors campagne |
| [Juggernaut_v2_Test.unity](C:/Users/pierr/git/Lit/Assets/Characters/3_Enemy/Juggernaut_v2/Juggernaut_v2_Test.unity:1) | laboratoire ou système historique, hors campagne |
| [FallingPhase.unity](C:/Users/pierr/git/Lit/Assets/FallingPhase_Legacy/FallingPhase.unity:1) | laboratoire ou système historique, hors campagne |

## Les onze fiches de fantômes

| Fiche | Réactions | Raccord de campagne |
|---|---|---|
| ghost_luc | 1 | Ghost_Luc.prefab |
| ghost_lucie | 1 | aucun contrôleur raccordé dans le graphe de campagne inspecté |
| ghost_scar | 0 | District_1_Cycle_Nina.unity; Ghost_Model_Scar.prefab |
| ghost_ulric | 1 | aucun contrôleur raccordé dans le graphe de campagne inspecté |
| ghost_nina | 0 | District_1_Cycle_Nina.unity |
| ghost_scientifique_fou | 0 | Enemy_Model_MadScientist.prefab |
| ghost_nora_ardent | 1 | Prefab_Ghost_Nora_Ardent.prefab |
| ghost_eloise_belmont | 1 | District_1_Cycle_Belmont.unity |
| ghost_agnes_clairmont | 1 | Prefab_Ghost_Agnes_Clairmont.prefab |
| ghost_enola_vivejoie | 0 | Prefab_Ghost_Enola_Vivejoie.prefab |
| ghost_etienne_belmont | 1 | Prefab_Ghost_Etienne_Belmont.prefab |

Les références de GhostController peuvent être redéfinies par une instance de scène ; cette table inventorie les fiches, elle ne remplace pas la résolution du dialogue de CycleInteraction. Nina et Scar utilisent notamment leurs dialogues de cycle, même avec zéro réaction générique.

## Objets et exposition directe dans la campagne

Inventaire des 175 Item. Une exposition directe peut être un objet de monde, un contenu de coffre, un inventaire de départ ou une récompense. Ce tableau ne prétend pas que chaque objet non exposé est inutile : les recettes et les créations par script restent des chemins indirects. Les détails de coordonnées, collisions et prix ne sont pas validés ici.

| Objet | Identifiant | Exposition directe observée | Savoir au ramassage / lecture |
|---|---|---|---|
| [Argile humide](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Argile%20humide.asset:1) | Item_Argile_humide | GameplaySessionRoot.prefab | — |
| [Argile seche](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Argile%20sèche.asset:1) | Item_Argile_seche | GameplaySessionRoot.prefab | — |
| [Balise](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Balise.asset:1) | Item_Balise | GameplaySessionRoot.prefab | — |
| [Bocal en verre ferme](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Bocal%20en%20verre%20fermé.asset:1) | Item_Bocal_verre_ferme | pas de référence directe observée ; vérifier usages indirects | — |
| [Bocal en verre sans couvercle](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Bocal%20en%20verre%20sans%20couvercle.asset:1) | Item_Bocal_verre_sans_couvercle | District_1_Enemies.unity | — |
| [Recueil](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Book_Gustave_Fairwell.asset:1) | Item_Book_Tomen_Fairwell | District_1_Interactives.unity; District_1_Rooms_Flammes.unity; GameplaySessionRoot.prefab | — |
| [Brasier de l'Espoir](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Building_Brasier%20de%20l'Espoir.asset:1) | brasierdelespoir | pas de référence directe observée ; vérifier usages indirects | — |
| [Catalyseur](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Building_Catalyseur.asset:1) | catalyseur | pas de référence directe observée ; vérifier usages indirects | — |
| [Coffre de la maison](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Building_Container_MaisonChest.asset:1) | maisonchest | GameplaySessionRoot.prefab; Maison_Chest.prefab | — |
| [Chiffon sale](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Chiffon%20sale.asset:1) | chiffonsale | District_1_Interactives.unity; GameplaySessionRoot.prefab; Maison_Chest.prefab; récompense reve.limbes.chambre | — |
| [Clou](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Clou.asset:1) | clou | GameplaySessionRoot.prefab | — |
| [Coffre](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Container_Chest_1.asset:1) | chest01 | pas de référence directe observée ; vérifier usages indirects | — |
| [Clé](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Container_Chest_1_Key.asset:1) | key01 | pas de référence directe observée ; vérifier usages indirects | — |
| [Coffre](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Container_Chest_2.asset:1) | chest02 | pas de référence directe observée ; vérifier usages indirects | — |
| [Clé](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Container_Chest_2_Key.asset:1) | key02 | pas de référence directe observée ; vérifier usages indirects | — |
| [Coffre](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Container_Chest_3.asset:1) | chest03 | pas de référence directe observée ; vérifier usages indirects | — |
| [Clé](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Container_Chest_3_Key.asset:1) | key03 | pas de référence directe observée ; vérifier usages indirects | — |
| [Coffre](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Container_Chest_Limbes_Secret.asset:1) | chest_limbes_secret | Reve_Limbes_Secrets.unity | — |
| [Tonneau](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Container_Tonneau.asset:1) | tonneau01 | District_1_Interactives.unity; GameplaySessionRoot.prefab; Maison_Chest.prefab | — |
| [Couvercle](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Couvercle.asset:1) | Item_Couvercle | pas de référence directe observée ; vérifier usages indirects | — |
| [Flamme](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Flame.asset:1) | flame | GameplaySessionRoot.prefab; départ Aurell; départ Lucian; départ Soren; départ Vesta | — |
| [Clé](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Gate_1_Key.asset:1) | key04 | District_1_Interactives.unity; GameplaySessionRoot.prefab | — |
| [Collier de Jon](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_JonCollar.asset:1) | jon_collar | District_1_Cycle_Luc.unity | Knowledge_JonLocationClue_1 |
| [Morceaux de papier](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Lannières%20en%20cuir.asset:1) | lannieres_en_cuir | GameplaySessionRoot.prefab | — |
| [Lapis-Lazuli](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Lapis-Lazuli.asset:1) | lapislazuli | GameplaySessionRoot.prefab; Maison_Chest.prefab | — |
| [Livre](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Livre_Les%20mémoires%20de%20Mélodie.asset:1) | item_livre | pas de référence directe observée ; vérifier usages indirects | — |
| [Livre](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Livre_Puzzle_Test_1.asset:1) | item_livre_puzzle_test_1 | pas de référence directe observée ; vérifier usages indirects | — |
| [Morceaux de papier](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Morceaux%20de%20papier.asset:1) | morceaux_de_papier | GameplaySessionRoot.prefab | — |
| [Collier](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Necklace_Jon.asset:1) | item_necklace_jon_table | pas de référence directe observée ; vérifier usages indirects | Knowledge_JonLocationClue_1 |
| [Note](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Note_Gustave_Table.asset:1) | item_note_gustave_table | District_1_Cycle_Gustave.unity; GameplaySessionRoot.prefab | — |
| [Histoire](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Note_Maelle_Cauldron.asset:1) | item_note_mael_story | District_1_Interactives.unity; GameplaySessionRoot.prefab | — |
| [Note](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Note_Maelle_Table.asset:1) | item_note_mael_table | GameplaySessionRoot.prefab | Knowledge_JonLocationClue_1 |
| [Note](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Note_Questionnement%20-%20Nourriture%20n°1.asset:1) | item_note_n_1 | pas de référence directe observée ; vérifier usages indirects | — |
| [Orbe doré](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Orbe/Item_Orbe_Gold.asset:1) | orbegold | GameplaySessionRoot.prefab; départ Aurell; départ Lucian; départ Soren; départ Vesta | — |
| [Orbe bleue](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Orbe_Blue/Item_Orbe%20bleu.asset:1) | orbebleu | GameplaySessionRoot.prefab; Maison_Chest.prefab | — |
| [Orbe rouge](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Orbe_Blue/Item_Orbe%20rouge.asset:1) | orberouge | District_1_Interactives.unity; GameplaySessionRoot.prefab; Maison_Chest.prefab | — |
| [Orbe violette](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Orbe_Violet/Item_Orbe%20violet.asset:1) | orbeviolette | GameplaySessionRoot.prefab | — |
| [Outils de crochetage](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_OutilsDeCrochetage.asset:1) | outils_de_crochetage | GameplaySessionRoot.prefab | — |
| [Parchemin](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Parchment_Notes%20sur%20les%20orbes%20n°1.asset:1) | item_parchment_orbes_1 | pas de référence directe observée ; vérifier usages indirects | — |
| [Planche de bois](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Planche%20de%20bois.asset:1) | planchedebois | GameplaySessionRoot.prefab | — |
| [Rubis](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Rubis.asset:1) | rubis | GameplaySessionRoot.prefab; Maison_Chest.prefab | — |
| [Item_Résidu lumineux](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Résidu%20lumineux.asset:1) |  | GameplaySessionRoot.prefab; Maison_Chest.prefab | — |
| [WoodShield](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Shield_WoodShield/Item_Shield_WoodShield.asset:1) | woodshield01 | pas de référence directe observée ; vérifier usages indirects | — |
| [Epée simple](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Weapon_Sword/Item_Weapon_Sword.asset:1) | sword01 | District_1_Corridor_Environment.unity; départ Aurell; départ Lucian; départ Soren; départ Vesta | — |
| [Registre des habitants - Quartier de la Lune Pleine](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/DistrictRegistries/Item_REG_LUNE_PLEINE.asset:1) | REG_LUNE_PLEINE | pas de référence directe observée ; vérifier usages indirects | — |
| [Registre des habitants - Quartier du Pic Noir](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/DistrictRegistries/Item_REG_PIC_NOIR.asset:1) | REG_PIC_NOIR | pas de référence directe observée ; vérifier usages indirects | — |
| [Petite tombe de Jon](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_JonGrave.asset:1) | jon_grave_marker | District_1_Cycle_Luc.unity | Knowledge_JonLocationClue_2 |
| [Chanson 1](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_Note_Archiviste_Chanson_1.asset:1) | Item_Note_Archiviste_Chanson_1 | pas de référence directe observée ; vérifier usages indirects | — |
| [Reflexion sur les jeunes et les traditions](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_Note_Mediateur_Archiviste_Reflexion%20sur%20les%20jeunes%20et%20les%20traditions.asset:1) | Item_Note_Mediateur_Archiviste_Reflexion sur les jeunes et les traditions | pas de référence directe observée ; vérifier usages indirects | — |
| [Réflexion sur l'indifférence des nouvelles générations](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_Parchment_Blue_001_IndifférenceCommune.asset:1) | READ_RED_004_Indifférence commune | District_1_Interactives.unity; GameplaySessionRoot.prefab | — |
| [Note pliée dans une fissure](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_001_PierreFroide.asset:1) | READ_BLUE_001_PierreFroide | pas de référence directe observée ; vérifier usages indirects | — |
| [Marge arrachée](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_002_MargeArrachee.asset:1) | READ_BLUE_002_MargeArrachee | pas de référence directe observée ; vérifier usages indirects | — |
| [Phrase gravée derrière une étagère](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_003_DerriereEtagere.asset:1) | READ_BLUE_003_DerriereEtagere | pas de référence directe observée ; vérifier usages indirects | — |
| [Billet retrouvé dans une reliure](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_004_ReliureCachee.asset:1) | READ_BLUE_004_ReliureCachee | pas de référence directe observée ; vérifier usages indirects | — |
| [Fragment taché de sel](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_005_SelSurLeBord.asset:1) | READ_BLUE_005_SelSurLeBord | pas de référence directe observée ; vérifier usages indirects | — |
| [Question sans réponse](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_006_QuestionSansReponse.asset:1) | READ_BLUE_006_QuestionSansReponse | pas de référence directe observée ; vérifier usages indirects | — |
| [Liste aux noms grattés](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_007_NomsGrattes.asset:1) | READ_BLUE_007_NomsGrattes | pas de référence directe observée ; vérifier usages indirects | — |
| [Note sous la chapelle](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_008_CelluleSousLaChapelle.asset:1) | READ_BLUE_008_CelluleSousLaChapelle | pas de référence directe observée ; vérifier usages indirects | — |
| [Journal d'un veilleur](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_009_VentDesCaves.asset:1) | READ_BLUE_009_VentDesCaves | pas de référence directe observée ; vérifier usages indirects | — |
| [Invitation à comparer](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_010_ComparerDeuxEncres.asset:1) | READ_BLUE_010_ComparerDeuxEncres | pas de référence directe observée ; vérifier usages indirects | — |
| [Chanson interrompue](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_011_ChansonOubliee.asset:1) | READ_BLUE_011_ChansonOubliee | pas de référence directe observée ; vérifier usages indirects | — |
| [Prière inachevée](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_012_EtoileDuMatin.asset:1) | READ_BLUE_012_EtoileDuMatin | pas de référence directe observée ; vérifier usages indirects | — |
| [Trace de corde](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_013_TraceDeCorde.asset:1) | READ_BLUE_013_TraceDeCorde | pas de référence directe observée ; vérifier usages indirects | — |
| [Carte sans route](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_014_CarteSansRoute.asset:1) | READ_BLUE_014_CarteSansRoute | pas de référence directe observée ; vérifier usages indirects | — |
| [Écoute derrière le mur](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_015_VoixSousLeChant.asset:1) | READ_BLUE_015_VoixSousLeChant | pas de référence directe observée ; vérifier usages indirects | — |
| [Lettre non envoyée](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_016_AubeNonDite.asset:1) | READ_BLUE_016_AubeNonDite | pas de référence directe observée ; vérifier usages indirects | — |
| [Registre décalé](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_017_RegistreDecale.asset:1) | READ_BLUE_017_RegistreDecale | pas de référence directe observée ; vérifier usages indirects | — |
| [Salle non cartographiée](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_018_SalleNonCartographiee.asset:1) | READ_BLUE_018_SalleNonCartographiee | pas de référence directe observée ; vérifier usages indirects | — |
| [Éclat de peinture](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_019_NomSousPeinture.asset:1) | READ_BLUE_019_NomSousPeinture | pas de référence directe observée ; vérifier usages indirects | — |
| [Fragment codé](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_BLUE_020_NePasDireLeMot.asset:1) | READ_BLUE_020_NePasDireLeMot | pas de référence directe observée ; vérifier usages indirects | — |
| [Contrôle des flammes nord](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_001_ControleFlammesNord.asset:1) | READ_GOLD_001_ControleFlamesNord | pas de référence directe observée ; vérifier usages indirects | — |
| [Avis de la troisième cour](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_002_TroisiemeCour.asset:1) | READ_GOLD_002_TroisiemeCour | pas de référence directe observée ; vérifier usages indirects | — |
| [Prière de stabilité](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_003_StabiliteQuotidienne.asset:1) | READ_GOLD_003_StabiliteQuotidienne | pas de référence directe observée ; vérifier usages indirects | — |
| [Registre du pain et du sel](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_004_RationPainSel.asset:1) | READ_GOLD_004_RationPainSel | pas de référence directe observée ; vérifier usages indirects | — |
| [Consigne du pont intérieur](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_005_PontInterieur.asset:1) | READ_GOLD_005_PontInterieur | pas de référence directe observée ; vérifier usages indirects | — |
| [Règlement du dortoir sud](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_006_DortoirSud.asset:1) | READ_GOLD_006_DortoirSud | pas de référence directe observée ; vérifier usages indirects | — |
| [Décret des clefs](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_007_ClefsEtSceaux.asset:1) | READ_GOLD_007_ClefsEtSceaux | pas de référence directe observée ; vérifier usages indirects | — |
| [Ordre de réparation](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_008_RempartOuest.asset:1) | READ_GOLD_008_RempartOuest | pas de référence directe observée ; vérifier usages indirects | — |
| [Office des portes](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_009_OfficeDesPortes.asset:1) | READ_GOLD_009_OfficeDesPortes | pas de référence directe observée ; vérifier usages indirects | — |
| [Inventaire des lampes](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_010_InventaireDesLampes.asset:1) | READ_GOLD_010_InventaireDesLampes | pas de référence directe observée ; vérifier usages indirects | — |
| [Instruction aux nouveaux gardes](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_011_ProtectionDuDedans.asset:1) | READ_GOLD_011_ProtectionDuDedans | pas de référence directe observée ; vérifier usages indirects | — |
| [Calendrier des offices](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_012_CalendrierDesOffices.asset:1) | READ_GOLD_012_CalendrierDesOffices | pas de référence directe observée ; vérifier usages indirects | — |
| [Rapport de citerne](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_013_CiterneBasse.asset:1) | READ_GOLD_013_CiterneBasse | pas de référence directe observée ; vérifier usages indirects | — |
| [Note de galerie instable](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_014_StabiliserLaGalerie.asset:1) | READ_GOLD_014_StabiliserLaGalerie | pas de référence directe observée ; vérifier usages indirects | — |
| [Tenue de cérémonie](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_015_TenueDeCeremonie.asset:1) | READ_GOLD_015_TenueDeCeremonie | pas de référence directe observée ; vérifier usages indirects | — |
| [Distribution des tours](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_016_DistributionDesTours.asset:1) | READ_GOLD_016_DistributionDesTours | pas de référence directe observée ; vérifier usages indirects | — |
| [Registre des naissances](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_017_ArchivesDesNaissances.asset:1) | READ_GOLD_017_ArchivesDesNaissances | pas de référence directe observée ; vérifier usages indirects | — |
| [Avis de réparation](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_018_PierreFendue.asset:1) | READ_GOLD_018_PierreFendue | pas de référence directe observée ; vérifier usages indirects | — |
| [Décret des escaliers](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_019_SilenceDesEscaliers.asset:1) | READ_GOLD_019_SilenceDesEscaliers | pas de référence directe observée ; vérifier usages indirects | — |
| [Après l'alerte](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_GOLD_020_OrdreApresAlerte.asset:1) | READ_GOLD_020_OrdreApresAlerte | pas de référence directe observée ; vérifier usages indirects | — |
| [Prière du passage](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_001_PassageConsent.asset:1) | READ_RED_001_PassageConsent | pas de référence directe observée ; vérifier usages indirects | — |
| [Note du troisième flame](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_002_TroisiemeFlamme.asset:1) | READ_RED_002_TroisiemeFlame | pas de référence directe observée ; vérifier usages indirects | — |
| [Consigne des conduits chauds](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_003_ConduitChaud.asset:1) | READ_RED_003_ConduitChaud | pas de référence directe observée ; vérifier usages indirects | — |
| [Lettre d'un prêtre ardent](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_005_LettreDuPretreArdent.asset:1) | READ_RED_005_LettreDuPretreArdent | pas de référence directe observée ; vérifier usages indirects | — |
| [La pierre ouverte](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_006_PierreOuverte.asset:1) | READ_RED_006_PierreOuverte | pas de référence directe observée ; vérifier usages indirects | — |
| [Cendre des noms](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_007_SangEtCendre.asset:1) | READ_RED_007_SangEtCendre | pas de référence directe observée ; vérifier usages indirects | — |
| [Salle des cordes](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_008_SalleDesCordes.asset:1) | READ_RED_008_SalleDesCordes | pas de référence directe observée ; vérifier usages indirects | — |
| [Chant des voix unies](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_009_VoixUnies.asset:1) | READ_RED_009_VoixUnies | pas de référence directe observée ; vérifier usages indirects | — |
| [Porte du renoncement](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_010_PorteSacrificielle.asset:1) | READ_RED_010_PorteSacrificielle | pas de référence directe observée ; vérifier usages indirects | — |
| [Avant le siège](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_011_AvantLeSiege.asset:1) | READ_RED_011_AvantLeSiege | pas de référence directe observée ; vérifier usages indirects | — |
| [Brasier des noms](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_012_BrasierDesNoms.asset:1) | READ_RED_012_BrasierDesNoms | pas de référence directe observée ; vérifier usages indirects | — |
| [Marche de veille](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_013_MarcheForcee.asset:1) | READ_RED_013_MarcheForcee | pas de référence directe observée ; vérifier usages indirects | — |
| [Chant des basses salles](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_014_ChantDesBassesSalles.asset:1) | READ_RED_014_ChantDesBassesSalles | pas de référence directe observée ; vérifier usages indirects | — |
| [Épreuve de la main](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_015_MainDansLaFlamme.asset:1) | READ_RED_015_MainDansLaFlamme | pas de référence directe observée ; vérifier usages indirects | — |
| [Note sur les roues cachées](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_016_RouesCachees.asset:1) | READ_RED_016_RouesCachees | pas de référence directe observée ; vérifier usages indirects | — |
| [Unité dans la crainte](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_017_UniteParLaCrainte.asset:1) | READ_RED_017_UniteParLaCrainte | pas de référence directe observée ; vérifier usages indirects | — |
| [La chambre des flux](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_018_ChambreDesFlux.asset:1) | READ_RED_018_ChambreDesFlux | pas de référence directe observée ; vérifier usages indirects | — |
| [Passage des Chanteurs](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_019_PassageDesChanteurs.asset:1) | READ_RED_019_PassageDesChanteurs | pas de référence directe observée ; vérifier usages indirects | — |
| [Dernière ouverture](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_RED_020_DerniereOuverture.asset:1) | READ_RED_020_DerniereOuverture | pas de référence directe observée ; vérifier usages indirects | — |
| [Chronique adoucie](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_002_ChroniqueAdoucie.asset:1) | READ_VIOLET_002_ChroniqueAdoucie | pas de référence directe observée ; vérifier usages indirects | — |
| [Instruction pour le vitrail du siège](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_003_VitrailDuSiege.asset:1) | READ_VIOLET_003_VitrailDuSiege | pas de référence directe observée ; vérifier usages indirects | — |
| [Conte du promontoire](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_004_ConteDuPromontoire.asset:1) | READ_VIOLET_004_ConteDuPromontoire | pas de référence directe observée ; vérifier usages indirects | — |
| [Les archives respirent](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_005_ArchivesRespirent.asset:1) | READ_VIOLET_005_ArchivesRespirent | pas de référence directe observée ; vérifier usages indirects | — |
| [Version pour la cour](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_006_VersionPourLaCour.asset:1) | READ_VIOLET_006_VersionPourLaCour | pas de référence directe observée ; vérifier usages indirects | — |
| [Fresque de la grande statue](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_007_FresqueDeLaStatue.asset:1) | READ_VIOLET_007_FresqueDeLaStatue | pas de référence directe observée ; vérifier usages indirects | — |
| [Sermon des remparts](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_008_SermonDesRemparts.asset:1) | READ_VIOLET_008_SermonDesRemparts | pas de référence directe observée ; vérifier usages indirects | — |
| [Chant des veilleurs](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_009_ChantDesVeilleurs.asset:1) | READ_VIOLET_009_ChantDesVeilleurs | pas de référence directe observée ; vérifier usages indirects | — |
| [Liste de noms corrigée](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_010_CorrectionDesNoms.asset:1) | READ_VIOLET_010_CorrectionDesNoms | pas de référence directe observée ; vérifier usages indirects | — |
| [Note au jeune prêcheur](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_011_DouteEtRepos.asset:1) | READ_VIOLET_011_DouteEtRepos | pas de référence directe observée ; vérifier usages indirects | — |
| [Cartes du dehors](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_012_SalleDesCartes.asset:1) | READ_VIOLET_012_SalleDesCartes | pas de référence directe observée ; vérifier usages indirects | — |
| [Troisième hiver](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_013_TroisiemeHiver.asset:1) | READ_VIOLET_013_TroisiemeHiver | pas de référence directe observée ; vérifier usages indirects | — |
| [Voix du siège](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_014_VoixDuSiege.asset:1) | READ_VIOLET_014_VoixDuSiege | pas de référence directe observée ; vérifier usages indirects | — |
| [Fable du sel](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_015_FableDuSel.asset:1) | READ_VIOLET_015_FableDuSel | pas de référence directe observée ; vérifier usages indirects | — |
| [Mémoire protectrice](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_016_MemoireProtectrice.asset:1) | READ_VIOLET_016_MemoireProtectrice | pas de référence directe observée ; vérifier usages indirects | — |
| [Masques des assaillants](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_017_MasqueDesDemoniaques.asset:1) | READ_VIOLET_017_MasqueDesDemoniaques | pas de référence directe observée ; vérifier usages indirects | — |
| [Parole et pierre](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_018_ParoleEtPierre.asset:1) | READ_VIOLET_018_ParoleEtPierre | pas de référence directe observée ; vérifier usages indirects | — |
| [Omission charitable](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_019_OmissionCharitable.asset:1) | READ_VIOLET_019_OmissionCharitable | pas de référence directe observée ; vérifier usages indirects | — |
| [Instruction des deux versions](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Readable/Item_READ_VIOLET_020_DeuxVersions.asset:1) | READ_VIOLET_020_DeuxVersions | pas de référence directe observée ; vérifier usages indirects | — |
| [Note](C:/Users/pierr/git/Lit/Assets/Interactive/Ghost/Ulric/Item_Note_Limbes_Climb.asset:1) | item_note_limbes_climb | Reve_Limbes_Secrets.unity; Reve_Limbes_Secrets.unity (override) | — |
| [Note](C:/Users/pierr/git/Lit/Assets/Interactive/Ghost/Ulric/Item_Note_Ulric.asset:1) | item_note_ulric | GameplaySessionRoot.prefab; Props_Note_Limbes_Smoke.prefab | Knowledge_UlricRemainsLocation |
| [Lettre manuscrite d'Édouard](C:/Users/pierr/git/Lit/Assets/Narrative/NinaCycle/Data/Item_Parchment_Edouard.asset:1) | item_edward | District_1_Cycle_Nina.unity | Knowledge_DilemmeEdouard |
| [Une visite sans panier](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_courts.optional.visit.asset:1) | courts.optional.visit | District_2_Courts_Secrets.unity; GameplaySessionRoot.prefab | Knowledge_courts.optional.visit |
| [Note — District 1 — le couloir ouest](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.01.asset:1) | secret.note.01 | District_2_Courts_Secrets.unity; GameplaySessionRoot.prefab | Knowledge_secret.passage.01 |
| [Note — District 1 — l'entrée et le couloir ouest](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.02.asset:1) | secret.note.02 | GameplaySessionRoot.prefab; Reve_Limbes_Secrets.unity | Knowledge_secret.passage.02 |
| [Note — Cours — Maison et galerie](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.03.asset:1) | secret.note.03 | District_2_Courts_Secrets.unity; GameplaySessionRoot.prefab | Knowledge_secret.passage.03 |
| [Note — Cours — Atelier et galerie](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.04.asset:1) | secret.note.04 | District_2_Courts_Secrets.unity; GameplaySessionRoot.prefab | Knowledge_secret.passage.04 |
| [Note — Cours — D'une maison à l'autre](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.05.asset:1) | secret.note.05 | District_2_Courts_Secrets.unity; GameplaySessionRoot.prefab | Knowledge_secret.passage.05 |
| [Note — Cours — La cour et la maison](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.06.asset:1) | secret.note.06 | District_2_Courts_Secrets.unity; GameplaySessionRoot.prefab | Knowledge_secret.passage.06 |
| [Note — Cours — La cour et l'atelier](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.07.asset:1) | secret.note.07 | District_1_SecretNotes.unity; GameplaySessionRoot.prefab | Knowledge_secret.passage.07 |
| [Note — Cours — Le recoin de la réserve](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.08.asset:1) | secret.note.08 | GameplaySessionRoot.prefab; Reve_Limbes_Secrets.unity | Knowledge_secret.passage.08 |
| [Note — Limbes — vers la terrasse gauche](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.09.asset:1) | secret.note.09 | District_2_Courts_Secrets.unity; GameplaySessionRoot.prefab | Knowledge_secret.passage.09 |
| [Note — Limbes — vers la terrasse droite](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Item_secret.note.10.asset:1) | secret.note.10 | GameplaySessionRoot.prefab; Reve_Limbes_Secrets.unity | Knowledge_secret.passage.10 |
| [Consigne de maintenance des conduits](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Ardent/Data/Readable_Ardent_conduit_instruction_read.asset:1) | district1.ardent.conduit_instruction_read | Prefab_Ardent_conduit_instruction_read.prefab | Knowledge_Ardent_conduit_instruction_read; Knowledge_Ardent_conduit_instruction_read |
| [Ruban familial d’Iris](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Ardent/Data/Readable_Ardent_iris_ribbon_found.asset:1) | district1.ardent.iris_ribbon_found | Prefab_Ardent_iris_ribbon_found.prefab | Knowledge_Ardent_iris_ribbon_found; Knowledge_Ardent_iris_ribbon_found |
| [Registre de transfert d’Iris](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Ardent/Data/Readable_Ardent_transfer_register_read.asset:1) | district1.ardent.transfer_register_read | Prefab_Ardent_transfer_register_read.prefab | Knowledge_Ardent_transfer_register_read; Knowledge_Ardent_transfer_register_read |
| [Note d’Étienne](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/Data/Item_Belmont_FamilyNote.asset:1) | note_belmont_etienne | District_1_Cycle_Belmont.unity | Knowledge_Belmont_family_note |
| [Relevé des conduits](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/Data/Item_Belmont_FlameMaintenance.asset:1) | releve_belmont_conduits | District_1_Cycle_Belmont.unity | Knowledge_Belmont_flame_maintenance |
| [Registre de relève](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/Data/Item_Belmont_ShiftRegister.asset:1) | registre_belmont_releve | District_1_Cycle_Belmont.unity | Knowledge_Belmont_shift_register |
| [Consigne du parcours ancien](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Clairmont/Data/Readable_Clairmont_old_route.asset:1) | district1.clairmont.old_route | Prefab_Clairmont_old_route.prefab | Knowledge_Clairmont_old_route; Knowledge_Clairmont_old_route |
| [Bordereau de transmission](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Clairmont/Data/Readable_Clairmont_relief_register.asset:1) | district1.clairmont.relief_register | Prefab_Clairmont_relief_register.prefab | Knowledge_Clairmont_relief_register; Knowledge_Clairmont_relief_register |
| [Note du frein de secours](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Etienne/Data/Readable_BrakeNote.asset:1) | district1.etienne.BrakeNote | District_1_Cycle_Etienne.unity | Knowledge_EmergencyBrake; Knowledge_EmergencyBrake |
| [Relevé des conduits de Veillée](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Etienne/Data/Readable_ConduitSurvey.asset:1) | district1.etienne.ConduitSurvey | District_1_Cycle_Etienne.unity | Knowledge_ConduitRoute; Knowledge_ConduitRoute |
| [Registre de passage de la relève](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Etienne/Data/Readable_ReliefRegister.asset:1) | district1.etienne.ReliefRegister | District_1_Cycle_Etienne.unity | Knowledge_ReliefPassed; Knowledge_ReliefPassed |
| [Le carnet de Alix](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/Data/Note_Courts_familyCycle_curiosity.asset:1) | district2.courts.family.curiosity | District_2_Courts.unity; GameplaySessionRoot.prefab | Knowledge_Courts_familyCycle_curiosity |
| [Les allers-retours](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/Data/Note_Courts_familyCycle_help.asset:1) | district2.courts.family.help | District_2_Courts.unity; GameplaySessionRoot.prefab | Knowledge_Courts_familyCycle_help |
| [Le billet d'Agathe](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/Data/Note_Courts_familyCycle_ties.asset:1) | district2.courts.family.ties | District_2_Courts.unity; GameplaySessionRoot.prefab | Knowledge_Courts_familyCycle_ties |
| [Les pierres dégagées](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/Data/Note_Courts_marginsCycle_made.asset:1) | district2.courts.margins.made | District_2_Courts.unity; GameplaySessionRoot.prefab | Knowledge_Courts_marginsCycle_made |
| [L'ouverture ancienne](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/Data/Note_Courts_marginsCycle_old.asset:1) | district2.courts.margins.old | District_2_Courts.unity; GameplaySessionRoot.prefab | Knowledge_Courts_marginsCycle_old |
| [Les paniers de la réserve](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/Data/Note_Courts_reserveCycle_baskets.asset:1) | district2.courts.reserve.baskets | District_2_Courts.unity; GameplaySessionRoot.prefab | Knowledge_Courts_reserveCycle_baskets |
| [Le registre des parts](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/Data/Note_Courts_reserveCycle_ledger.asset:1) | district2.courts.reserve.ledger | District_2_Courts.unity; GameplaySessionRoot.prefab | Knowledge_Courts_reserveCycle_ledger |
| [Le trajet de Abel](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/Data/Note_Courts_visitsCycle_silas.asset:1) | district2.courts.visits.silas | District_2_Courts.unity; GameplaySessionRoot.prefab | Knowledge_Courts_visitsCycle_silas |
| [Deux visites ordinaires](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/Data/Note_Courts_visitsCycle_visits.asset:1) | district2.courts.visits.visits | District_2_Courts.unity; GameplaySessionRoot.prefab | Knowledge_Courts_visitsCycle_visits |
| [Des cellules sous les chapelles](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Completion/Data/Preuve_Captivite_0.asset:1) | reve.limbes.captivite.proof.0 | GameplaySessionRoot.prefab; Reve_Limbes_Cycle_Captivite.unity | Knowledge_Captivite_0; Knowledge_Captivite_0 |
| [La lignée avant les noms](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Completion/Data/Preuve_Captivite_1.asset:1) | reve.limbes.captivite.proof.1 | GameplaySessionRoot.prefab; Reve_Limbes_Cycle_Captivite.unity | Knowledge_Captivite_1; Knowledge_Captivite_1 |
| [Des âmes venues des Enfers](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Completion/Data/Preuve_Captivite_2.asset:1) | reve.limbes.captivite.proof.2 | GameplaySessionRoot.prefab; Reve_Limbes_Cycle_Captivite.unity | Knowledge_Captivite_2; Knowledge_Captivite_2 |
| [Les deux faces d'une arche](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Completion/Data/Preuve_Creation_0.asset:1) | reve.limbes.creation.proof.0 | GameplaySessionRoot.prefab; Reve_Limbes_Cycle_Creation.unity | Knowledge_Creation_0; Knowledge_Creation_0 |
| [Le nom d'Azazel](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Completion/Data/Preuve_Creation_1.asset:1) | reve.limbes.creation.proof.1 | GameplaySessionRoot.prefab; Reve_Limbes_Cycle_Creation.unity | Knowledge_Creation_1; Knowledge_Creation_1 |
| [Une création imparfaite](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Completion/Data/Preuve_Creation_2.asset:1) | reve.limbes.creation.proof.2 | GameplaySessionRoot.prefab; Reve_Limbes_Cycle_Creation.unity | Knowledge_Creation_2; Knowledge_Creation_2 |
| [Le visage sous la protection](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Completion/Data/Preuve_Invocation_0.asset:1) | reve.limbes.invocation.proof.0 | GameplaySessionRoot.prefab; Reve_Limbes_Cycle_Invocation.unity | Knowledge_Invocation_0; Knowledge_Invocation_0 |
| [Les voix réunies](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Completion/Data/Preuve_Invocation_1.asset:1) | reve.limbes.invocation.proof.1 | GameplaySessionRoot.prefab; Reve_Limbes_Cycle_Invocation.unity | Knowledge_Invocation_1; Knowledge_Invocation_1 |
| [Le passage interrompu](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Completion/Data/Preuve_Invocation_2.asset:1) | reve.limbes.invocation.proof.2 | GameplaySessionRoot.prefab; Reve_Limbes_Cycle_Invocation.unity | Knowledge_Invocation_2; Knowledge_Invocation_2 |
| [La fenêtre n'a pas bougé](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Cycles/Chambre/Data/Item_0.asset:1) | reve.limbes.chambre.fragment.0 | Fragment_0.prefab; GameplaySessionRoot.prefab | Knowledge_0; Knowledge_0 |
| [Un seuil sans mur](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Cycles/Seuil/Data/Item_0.asset:1) | reve.limbes.seuil.fragment.0 | Fragment_0.prefab; GameplaySessionRoot.prefab | Knowledge_0; Knowledge_0 |
| [La même entaille](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Cycles/Seuil/Data/Item_1.asset:1) | reve.limbes.seuil.fragment.1 | Fragment_1.prefab; GameplaySessionRoot.prefab | Knowledge_1; Knowledge_1 |
| [Des pierres qui se reconnaissent](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Cycles/Seuil/Data/Item_2.asset:1) | reve.limbes.seuil.fragment.2 | Fragment_2.prefab; GameplaySessionRoot.prefab | Knowledge_2; Knowledge_2 |

## Contrôles effectués et limites

- Cinq manifestes et 53 scènes de build : cohérence des destinations de chargement contrôlée statiquement.
- Dix-huit cycles et 112 étapes : unicité des IDs de cycles, présence de terminaux, références Step locales et absence de cycle dans ce graphe de prérequis.
- Scène binaire de la nef : 942 objets décodés en lecture seule, dont le raccord de sortie Clairmont et son TrouEtroit.
- Deux sources de Flame portant reve.limbes.expansion.Creation.peak repérées dans deux scènes chargées ensemble.
- Assets examinés tels qu’enregistrés sur disque ; changements non enregistrés dans l’éditeur exclus.
- Aucun résultat de compilation, parcours physique, réseau ou ressenti n’est déduit de ces contrôles. Les scripts temporaires et exports détaillés restent sous Library/ProgressionAudit et ne modifient pas les assets de jeu.
