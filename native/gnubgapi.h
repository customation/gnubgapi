/*
 * GNU Backgammon API (initial stub)
 *
 * This header defines a minimal, stable C ABI for a future P/Invoke layer.
 * The initial implementation is intentionally small and returns
 * GNUBGAPI_E_NOT_IMPLEMENTED for most calls until the shim is wired up.
 */
#ifndef GNUBGAPI_H
#define GNUBGAPI_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32) || defined(__CYGWIN__)
  #if defined(GNUBGAPI_BUILD)
    #define GNUBGAPI_EXPORT __declspec(dllexport)
  #elif defined(GNUBGAPI_STATIC)
    #define GNUBGAPI_EXPORT
  #else
    #define GNUBGAPI_EXPORT __declspec(dllimport)
  #endif
#else
  #define GNUBGAPI_EXPORT __attribute__((visibility("default")))
#endif

#define GNUBGAPI_VERSION_MAJOR 0
#define GNUBGAPI_VERSION_MINOR 1
#define GNUBGAPI_VERSION_PATCH 0

typedef enum gnubgapi_status {
    GNUBGAPI_OK = 0,
    GNUBGAPI_E_INVALID_ARGUMENT = 1,
    GNUBGAPI_E_NOT_INITIALIZED = 2,
    GNUBGAPI_E_NOT_IMPLEMENTED = 3,
    GNUBGAPI_E_INTERNAL = 4
} gnubgapi_status;

typedef struct gnubgapi_context gnubgapi_context;

GNUBGAPI_EXPORT void gnubgapi_get_version(uint32_t *major, uint32_t *minor, uint32_t *patch);
GNUBGAPI_EXPORT const char *gnubgapi_get_last_error(void);

GNUBGAPI_EXPORT gnubgapi_context *gnubgapi_create(void);
GNUBGAPI_EXPORT void gnubgapi_destroy(gnubgapi_context *ctx);

GNUBGAPI_EXPORT gnubgapi_status gnubgapi_init(
    gnubgapi_context *ctx,
    const char *weights_path,
    const char *weights_binary_path,
    const char *data_dir,
    int no_bearoff
);

GNUBGAPI_EXPORT void gnubgapi_shutdown(gnubgapi_context *ctx);

/*
 * Evaluate a position. If match_id is provided, cube/turn info is taken
 * from it; otherwise a default money-cube state is used.
 *
 * out_equity returns cubeless equity.
 * out_cubeful_equity (optional) returns cubeful equity.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_evaluate_position(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    double *out_equity,
    double *out_cubeful_equity
);

/*
 * Evaluate a position with configurable search depth.
 *
 * n_plies controls the look-ahead depth:
 *   0 = neural-net evaluation only (same as gnubgapi_evaluate_position)
 *   2 = "world class" level (2-ply search)
 *
 * out_equity returns cubeless equity from the on-roll player's perspective.
 * out_cubeful_equity (optional) returns cubeful equity.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_evaluate_position_plied(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    uint32_t n_plies,
    double *out_equity,
    double *out_cubeful_equity
);

/*
 * Output layout for full evaluation and rollout (7 doubles):
 *   [0] P(win)
 *   [1] P(win gammon)
 *   [2] P(win backgammon)
 *   [3] P(lose gammon)
 *   [4] P(lose backgammon)
 *   [5] Cubeless equity
 *   [6] Cubeful equity
 */
#define GNUBGAPI_NUM_ROLLOUT_OUTPUTS 7

/*
 * Evaluate a position and return all 7 neural-net outputs.
 * Same position/match_id semantics as gnubgapi_evaluate_position.
 * out_output must point to a double[7] array.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_evaluate_position_full(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    double out_output[GNUBGAPI_NUM_ROLLOUT_OUTPUTS]
);

/*
 * Evaluate a position with configurable search depth and return all 7
 * outputs (5 probabilities + cubeless + cubeful equity).
 *
 * Combines gnubgapi_evaluate_position_plied (n-ply search) with
 * gnubgapi_evaluate_position_full (all outputs).
 *
 * out_output must point to a double[7] array.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_evaluate_position_full_plied(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    uint32_t n_plies,
    double out_output[GNUBGAPI_NUM_ROLLOUT_OUTPUTS]
);

typedef struct gnubgapi_rollout_settings {
    uint32_t n_trials;           /* Number of games to simulate (default: 1296) */
    int32_t  cubeful;            /* 1 = cubeful rollout, 0 = cubeless (default: 1) */
    int32_t  variance_reduction; /* 1 = enable variance reduction (default: 1) */
    uint32_t chequer_plies;      /* Plies for chequer play during rollout (default: 0) */
    uint32_t cube_plies;         /* Plies for cube decisions during rollout (default: 2) */
    uint32_t seed;               /* Random seed (0 = default) */
    int32_t  truncate;           /* 1 = truncate with bearoff eval (default: 1) */
    uint32_t truncate_plies;     /* Ply at which to truncate (default: 10) */
} gnubgapi_rollout_settings;

/* Fill settings with sensible defaults. */
GNUBGAPI_EXPORT void gnubgapi_rollout_settings_default(gnubgapi_rollout_settings *settings);

/*
 * Run a rollout for the given position.
 *
 * settings may be NULL to use defaults.
 * out_output must point to a double[7] array — receives mean values.
 * out_std_dev must point to a double[7] array — receives standard deviations.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_rollout_position(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    const gnubgapi_rollout_settings *settings,
    double out_output[GNUBGAPI_NUM_ROLLOUT_OUTPUTS],
    double out_std_dev[GNUBGAPI_NUM_ROLLOUT_OUTPUTS]
);

/* ------------------------------------------------------------------ */
/* Move generation API                                                */
/* ------------------------------------------------------------------ */

#define GNUBGAPI_MAX_MOVES 3060
#define GNUBGAPI_MOVE_STEPS 8

/*
 * A single generated move: up to 4 src-dest pairs (int[8], -1 terminated)
 * plus the resulting position_id after the move is applied and sides are
 * swapped (ready for the next player's evaluation).
 */
typedef struct gnubgapi_move {
    int an_move[GNUBGAPI_MOVE_STEPS];
    char result_position_id[16];   /* 14 chars + '\0' + pad */
    unsigned int n_submoves;       /* number of submoves (1-4) */
    unsigned int pips;             /* total pips moved */
} gnubgapi_move;

/*
 * A move with its GnuBG evaluation (returned by generate_moves_with_eval).
 */
typedef struct gnubgapi_scored_move {
    gnubgapi_move move;
    double equity;                 /* cubeful equity from on-roll perspective */
    double probs[5];               /* P(win), P(winG), P(winBG), P(loseG), P(loseBG) */
} gnubgapi_scored_move;

/*
 * Generate all legal moves for a position and dice roll.
 *
 * position_id: current position (on-roll player in anBoard[1]).
 * die1, die2: dice values (1-6).
 * out_moves: caller-allocated array (at least GNUBGAPI_MAX_MOVES elements).
 * out_count: receives the number of legal moves generated.
 *
 * Each move's result_position_id has sides swapped so the NEXT player
 * is in anBoard[1], ready for evaluation from their perspective.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_generate_moves(
    gnubgapi_context *ctx,
    const char *position_id,
    int die1,
    int die2,
    gnubgapi_move *out_moves,
    uint32_t *out_count
);

/*
 * Apply a raw move (int[8]) to a position and return the resulting
 * position_id with sides swapped (ready for the next player).
 * out_position_id must be at least 16 bytes.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_apply_move(
    gnubgapi_context *ctx,
    const char *position_id,
    const int an_move[GNUBGAPI_MOVE_STEPS],
    char *out_position_id
);

/*
 * Find the single best move using GnuBG's search.
 *
 * n_plies: 0 = neural-net only, 2 = world-class search.
 * out_move: receives the best move and resulting position_id.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_find_best_move(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    int die1,
    int die2,
    uint32_t n_plies,
    gnubgapi_move *out_move
);

/*
 * Generate all legal moves with evaluations, sorted best-first.
 *
 * n_plies: evaluation depth (0 or 2).
 * out_moves: caller-allocated array (at least GNUBGAPI_MAX_MOVES elements).
 * out_count: receives the number of moves.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_generate_moves_with_eval(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    int die1,
    int die2,
    uint32_t n_plies,
    gnubgapi_scored_move *out_moves,
    uint32_t *out_count
);

/* ------------------------------------------------------------------ */
/* Game analysis API                                                  */
/* ------------------------------------------------------------------ */

/*
 * A single turn in a recorded game, for analysis input.
 *
 * player: 0 or 1 (which player was on roll).
 * die1, die2: dice values (1-6).
 * an_move: move as from-to pairs from on-roll perspective (0-indexed
 *          points, bar=24), -1 terminated.  All -1 = no legal move.
 */
typedef struct gnubgapi_game_turn {
    int player;
    int die1, die2;
    int an_move[GNUBGAPI_MOVE_STEPS];
} gnubgapi_game_turn;

/* Skill levels (matches GnuBG's skilltype enum) */
#define GNUBGAPI_SKILL_VERYBAD   0
#define GNUBGAPI_SKILL_BAD       1
#define GNUBGAPI_SKILL_DOUBTFUL  2
#define GNUBGAPI_SKILL_NONE      3
#define GNUBGAPI_N_SKILLS        4

/*
 * Analysis result for a game — mirrors GnuBG's statcontext for
 * chequerplay only (no cube or luck analysis).
 */
typedef struct gnubgapi_analysis_result {
    int total_moves[2];                         /* total moves per player */
    int unforced_moves[2];                      /* moves with >1 legal option */
    int skill_counts[2][GNUBGAPI_N_SKILLS];     /* [player][skill] counts */
    float total_error[2];                       /* accumulated equity loss */
    float error_per_move[2];                    /* total_error / unforced_moves */
    float mpr[2];                               /* millipoints per move */
    char rating[2][32];                         /* "Beginner" .. "Super Grandmaster" */
    int n_games;
} gnubgapi_analysis_result;

/*
 * Analyse a complete game from structured turn data.
 *
 * Walks each turn, evaluates all legal moves with FindnSaveBestMoves,
 * compares the played move against GnuBG's best, and accumulates
 * chequerplay error statistics using GnuBG's exact skill thresholds.
 *
 * turns: array of game turns in order.
 * num_turns: length of the turns array.
 * n_plies: evaluation depth (0 = neural-net only, 2 = world-class).
 * out: receives the analysis result.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_analyse_game(
    gnubgapi_context *ctx,
    const gnubgapi_game_turn *turns,
    uint32_t num_turns,
    uint32_t n_plies,
    gnubgapi_analysis_result *out
);

/*
 * Analyse a game from a Jellyfish .mat file.
 *
 * Parses the .mat file, extracts turns, and calls gnubgapi_analyse_game
 * internally — guaranteed to produce identical results.
 *
 * mat_path: path to the .mat file.
 * n_plies: evaluation depth (0 or 2).
 * out: receives the analysis result.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_analyse_mat(
    gnubgapi_context *ctx,
    const char *mat_path,
    uint32_t n_plies,
    gnubgapi_analysis_result *out
);

/* ------------------------------------------------------------------ */
/* Cube decision API                                                  */
/* ------------------------------------------------------------------ */

/*
 * Cube-decision result produced by gnubg's GeneralCubeDecisionE +
 * FindCubeDecision pair. All equities are from the offerer's
 * (player-on-roll's) perspective and, in match play, normalized to
 * money-equity space (mwc2eq).
 */
typedef struct gnubgapi_cube_decision_result {
    /*
     * 2x7 cubeful outputs:
     *   Row 0 = no-double position
     *   Row 1 = double-take position (cube has been doubled and taken)
     *
     * Each row has the same layout as gnubgapi_evaluate_position_full:
     *   [0] P(win)
     *   [1] P(win gammon)
     *   [2] P(win backgammon)
     *   [3] P(lose gammon)
     *   [4] P(lose backgammon)
     *   [5] Cubeless equity
     *   [6] Cubeful equity
     */
    double cubeful_outputs[2][GNUBGAPI_NUM_ROLLOUT_OUTPUTS];

    /*
     * 4-element equity summary from the offerer's perspective:
     *   [0] OPTIMAL  — equity of the right decision
     *   [1] NODOUBLE — equity if the offerer does not double
     *   [2] TAKE     — equity if doubled and taken
     *   [3] DROP     — equity if doubled and dropped (+1 in money games,
     *                  match-score-normalized in match play)
     */
    double equities[4];

    /*
     * gnubg's `cubedecision` enum value. See GNUBGAPI_CUBE_DECISION_*
     * constants below for the stable wire format. Callers that don't
     * care about every distinction can collapse the optional/redouble
     * variants down to the four basic outcomes (DOUBLE/TAKE,
     * DOUBLE/PASS, NODOUBLE/TAKE, TOOGOOD/PASS).
     */
    int32_t decision;
} gnubgapi_cube_decision_result;

/*
 * cubedecision enum values mirrored from gnubg's eval.h. The integer
 * values are stable across gnubgapi versions; callers should reference
 * these constants rather than the underlying enum ordinals.
 */
#define GNUBGAPI_CUBE_DECISION_DOUBLE_TAKE             0
#define GNUBGAPI_CUBE_DECISION_DOUBLE_PASS             1
#define GNUBGAPI_CUBE_DECISION_NODOUBLE_TAKE           2
#define GNUBGAPI_CUBE_DECISION_TOOGOOD_TAKE            3
#define GNUBGAPI_CUBE_DECISION_TOOGOOD_PASS            4
#define GNUBGAPI_CUBE_DECISION_DOUBLE_BEAVER           5
#define GNUBGAPI_CUBE_DECISION_NODOUBLE_BEAVER         6
#define GNUBGAPI_CUBE_DECISION_REDOUBLE_TAKE           7
#define GNUBGAPI_CUBE_DECISION_REDOUBLE_PASS           8
#define GNUBGAPI_CUBE_DECISION_NO_REDOUBLE_TAKE        9
#define GNUBGAPI_CUBE_DECISION_TOOGOODRE_TAKE         10
#define GNUBGAPI_CUBE_DECISION_TOOGOODRE_PASS         11
#define GNUBGAPI_CUBE_DECISION_NO_REDOUBLE_BEAVER     12
#define GNUBGAPI_CUBE_DECISION_NODOUBLE_DEADCUBE      13
#define GNUBGAPI_CUBE_DECISION_NO_REDOUBLE_DEADCUBE   14
#define GNUBGAPI_CUBE_DECISION_NOT_AVAILABLE          15
#define GNUBGAPI_CUBE_DECISION_OPTIONAL_DOUBLE_TAKE   16
#define GNUBGAPI_CUBE_DECISION_OPTIONAL_REDOUBLE_TAKE 17
#define GNUBGAPI_CUBE_DECISION_OPTIONAL_DOUBLE_BEAVER 18
#define GNUBGAPI_CUBE_DECISION_OPTIONAL_DOUBLE_PASS   19
#define GNUBGAPI_CUBE_DECISION_OPTIONAL_REDOUBLE_PASS 20

/*
 * Evaluate the cube decision at the given position.
 *
 * Runs GeneralCubeDecisionE (which produces the 2x7 cubeful outputs)
 * followed by FindCubeDecision (which derives the 4-element equity
 * summary and the recommended cubedecision enum value).
 *
 * n_plies: evaluation depth (0 = neural-net only, 2 = world-class).
 * out: receives the full cube-decision result.
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_evaluate_cube_decision(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    uint32_t n_plies,
    gnubgapi_cube_decision_result *out
);

/* ------------------------------------------------------------------ */
/* Feature encoding API                                               */
/* ------------------------------------------------------------------ */

#define GNUBGAPI_FEATURE_DIM 248

/*
 * Compute 248 neural-net input features from a position_id.
 *
 * Layout: [bottom_base(100) | bottom_contact(24) |
 *          top_base(100)    | top_contact(24)]
 *
 * Features are always from Bottom's perspective regardless of who is
 * on roll.  The is_top_on_roll flag controls board mapping only.
 *
 * is_top_on_roll: 0 = bottom is on roll, 1 = top is on roll.
 * out_features: caller-allocated float[GNUBGAPI_FEATURE_DIM].
 */
GNUBGAPI_EXPORT gnubgapi_status gnubgapi_position_to_features(
    gnubgapi_context *ctx,
    const char *position_id,
    int is_top_on_roll,
    float *out_features
);

#ifdef __cplusplus
}
#endif

#endif
